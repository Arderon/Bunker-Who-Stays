using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Bunker.Content;
using Bunker.Core.Effects;

namespace Bunker.Core
{
    // The authoritative brain of a single game session.
    // Pure C# (no MonoBehaviour) — UI and networking subscribe to its events
    // and call its public methods, never touch player data directly.
    public class GameSession
    {
        public List<PlayerData> Players { get; private set; }
        public int CurrentRound { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.Lobby;

        private readonly GameSessionConfig _config;
        private readonly CharacterCardGenerator _cardGenerator;
        private readonly TurnOrderService _turnOrder = new();
        private readonly SpecialCardEffectRegistry _effectRegistry = new();
        private readonly Dictionary<string, string> _currentVotes = new(); // voterId -> targetId
        private List<string> _tiebreakerCandidateIds; // null when not in a tiebreaker
        private int _tiebreakerAttempts;
        private const int MaxTiebreakerAttempts = 1; // one re-vote, then give up


        // Fired whenever the phase transitions. UI listens to this to switch screens.
        public event Action<GamePhase> OnPhaseChanged;

        // Fired when a specific player is eliminated at the end of a round.
        public event Action<PlayerData> OnPlayerEliminated;

        // Fired once the win condition is reached, with the final survivors.
        public event Action<List<PlayerData>> OnGameOver;

        // Fired when a round officially starts (useful for UI to reset round-scoped state).
        public event Action<int> OnRoundStarted;

        // Fired whenever a player reveals one of their traits.
        // UI uses this to update that player's visible card.
        public event Action<PlayerData, CharacterTrait> OnTraitRevealed;

        // Fired when every active player has had a turn this round
        // (a full pass through the reveal order is complete).
        public event Action OnRevealPassCompleted;

        // Fired when a special card is successfully used, for UI feedback
        // (e.g. "Player X used their special card").
        public event Action<PlayerData, SpecialCard> OnSpecialCardUsed;

        // Fired when the discussion timer should start; the actual countdown
        // is owned by the caller (UI/network), GameSession only marks the phase.
        public event Action<int> OnDiscussionStarted; // duration in seconds, chosen by caller

        // Fired with the full result once a round's voting is resolved.
        public event Action<VotingResult> OnVotingResolved;

        // Fired once the game ends, with the full reason + survivor list.
        // Replaces the simpler List<PlayerData>-only event from section 1.3.
        public event Action<GameOverResult> OnGameOverResolved;

        public string CurrentTurnPlayerId => _turnOrder.CurrentPlayerId;

        public GameSession(
            List<PlayerData> players,
            GameSessionConfig config,
            CharacterCardGenerator cardGenerator)
        {
            if (players == null || players.Count == 0)
                throw new ArgumentException("GameSession requires at least one player.");

            Players = players;
            _config = config;
            _cardGenerator = cardGenerator;
        }

        // --- Pre-start validation --------------------------------------------

        // Called by the lobby UI before enabling the "Start" button, and again
        // defensively inside StartGame itself.
        public GameStartValidationResult ValidateCanStart()
        {
            if (Phase != GamePhase.Lobby)
                return GameStartValidationResult.Fail("Game has already started.");

            if (Players.Count == 0)
                return GameStartValidationResult.Fail("No players in the lobby.");

            if (_config.SurvivorsTarget <= 0)
                return GameStartValidationResult.Fail("Survivors target must be at least 1.");

            // Need strictly more players than the target, otherwise there is
            // nothing to play — the game would already be "won" at round 0.
            if (Players.Count <= _config.SurvivorsTarget)
            {
                return GameStartValidationResult.Fail(
                    $"Need more than {_config.SurvivorsTarget} players to start " +
                    $"(currently {Players.Count}).");
            }

            // Content sanity check: every player must have a full set of traits
            // ready to be dealt. Catches a misconfigured/empty trait pool early
            // instead of failing deep inside CharacterCardGenerator.
            if (!_cardGenerator.HasEnoughContentFor(Players.Count))
            {
                return GameStartValidationResult.Fail("Not enough card content configured for this many players.");
            }

            return GameStartValidationResult.Ok();
        }

        public void StartGame()
        {
            var validation = ValidateCanStart();
            if (!validation.CanStart)
            {
                Debug.LogError($"[GameSession] Cannot start game: {validation.FailReason}");
                return;
            }

            SetPhase(GamePhase.Dealing);
            _cardGenerator.DealToPlayers(Players);
            _turnOrder.Initialize(Players);

            CurrentRound = 0;
            StartNextRound();
        }

        // --- Lifecycle -----------------------------------------------------

        private void StartNextRound()
        {
            CurrentRound++;

            foreach (var player in ActivePlayers())
            {
                player.ResetPerRoundFlags();
            }

            _currentVotes.Clear();
            _turnOrder.RebuildForRound(CurrentRound, ActivePlayers());

            SetPhase(GamePhase.Reveal);
            OnRoundStarted?.Invoke(CurrentRound);
        }

        // --- Reveal phase ----------------------------------------------------

        // Attempts to reveal one trait category for the player whose turn it currently is.
        // Returns false (and does nothing) if the request is invalid, so the caller
        // (UI / network RPC handler) can show appropriate feedback instead of crashing.
        public bool RevealNextTrait(string playerId, CardCategory category)
        {
            if (Phase != GamePhase.Reveal)
            {
                Debug.LogWarning("[GameSession] RevealNextTrait called outside of Reveal phase.");
                return false;
            }

            if (!_turnOrder.IsPlayersTurn(playerId))
            {
                Debug.LogWarning($"[GameSession] It is not {playerId}'s turn.");
                return false;
            }

            var player = GetPlayer(playerId);
            if (player == null || player.IsEliminated)
            {
                Debug.LogWarning($"[GameSession] Unknown or eliminated player: {playerId}");
                return false;
            }

            if (player.IsCategoryRevealed(category))
            {
                Debug.LogWarning($"[GameSession] {playerId} already revealed {category}.");
                return false;
            }

            var trait = player.GetTrait(category);
            if (trait == null)
            {
                Debug.LogWarning($"[GameSession] {playerId} has no trait for category {category}.");
                return false;
            }

            player.RevealCategory(category);
            OnTraitRevealed?.Invoke(player, trait);

            AdvanceToNextTurn();
            return true;
        }

        private void AdvanceToNextTurn()
        {
            bool passContinues = _turnOrder.AdvanceTurn();

            if (!passContinues)
            {
                OnRevealPassCompleted?.Invoke();
                // Reveal phase ends after one full pass; the caller (UI/network)
                // decides when to actually transition to Discussion.
                // See note below.
            }
        }

        // Convenience check for the UI/network layer to know if a full reveal
        // pass has finished for all active players this round.
        public bool HasCompletedRevealPass()
        {
            return ActivePlayers().All(p => p.RevealedCategories.Count > 0);
        }

        // --- Phase transitions ---------------------------------------------
        // These are intentionally simple setters; concrete round logic
        // (reveal order, voting rules) lives in later sections (1.4, 1.5, 1.6)
        // and will call into these transitions.

        public void SetPhase(GamePhase newPhase)
        {
            Phase = newPhase;
            OnPhaseChanged?.Invoke(newPhase);
        }

        // --- Discussion phase -------------------------------------------------

        // Called once the reveal pass is done (see OnRevealPassCompleted from 1.4).
        // durationSeconds is passed by the caller since GameSession doesn't own real time.
        public void StartDiscussionPhase(int durationSeconds)
        {
            if (Phase != GamePhase.Reveal)
            {
                Debug.LogWarning("[GameSession] StartDiscussionPhase called outside of Reveal phase.");
                return;
            }

            SetPhase(GamePhase.Discussion);
            OnDiscussionStarted?.Invoke(durationSeconds);
        }

        // Called by the caller's timer once discussion time is up (or all players
        // signal ready — that policy lives outside GameSession).
        public void StartVotingPhase()
        {
            if (Phase != GamePhase.Discussion)
            {
                Debug.LogWarning("[GameSession] StartVotingPhase called outside of Discussion phase.");
                return;
            }

            _tiebreakerCandidateIds = null;
            _tiebreakerAttempts = 0;
            _currentVotes.Clear();
            SetPhase(GamePhase.Voting);
        }

        // --- Voting  ------------------------------------------------------------

        public bool CastVote(string voterPlayerId, string targetPlayerId)
        {
            bool inTiebreaker = Phase == GamePhase.VotingTiebreaker;

            if (Phase != GamePhase.Voting && !inTiebreaker)
            {
                Debug.LogWarning("[GameSession] Vote cast outside of Voting phase, ignored.");
                return false;
            }

            var voter = GetPlayer(voterPlayerId);
            if (voter == null || voter.IsEliminated)
            {
                Debug.LogWarning($"[GameSession] Invalid voter: {voterPlayerId}");
                return false;
            }

            if (voterPlayerId == targetPlayerId)
            {
                Debug.LogWarning("[GameSession] Self-voting is not allowed.");
                return false;
            }

            var target = GetPlayer(targetPlayerId);
            if (target == null || target.IsEliminated)
            {
                Debug.LogWarning($"[GameSession] Invalid vote target: {targetPlayerId}");
                return false;
            }

            if (target.HasVoteImmunityThisRound)
            {
                Debug.LogWarning($"[GameSession] {targetPlayerId} is immune this round, vote rejected.");
                return false;
            }

            // During a tiebreaker, votes may only go to the tied candidates
            // from the previous round of voting.
            if (inTiebreaker && !_tiebreakerCandidateIds.Contains(targetPlayerId))
            {
                Debug.LogWarning("[GameSession] Vote target is not part of the tiebreaker candidates.");
                return false;
            }

            _currentVotes[voterPlayerId] = targetPlayerId;
            return true;
        }

        // Called by the caller once every active player has voted (or a voting
        // timer expired). Resolves the round and transitions phases accordingly.
        public VotingResult ResolveVotes()
        {
            var result = TallyVotes();
            OnVotingResolved?.Invoke(result);

            switch (result.ResultType)
            {
                case VotingResult.Outcome.PlayerEliminated:
                    result.EliminatedPlayer.IsEliminated = true;
                    OnPlayerEliminated?.Invoke(result.EliminatedPlayer);
                    FinishRound();
                    break;

                case VotingResult.Outcome.TieRequiresRevote:
                    _tiebreakerCandidateIds = result.TiedCandidates.Select(p => p.PlayerId).ToList();
                    _tiebreakerAttempts++;
                    _currentVotes.Clear();
                    SetPhase(GamePhase.VotingTiebreaker);
                    break;

                case VotingResult.Outcome.TieUnresolvedNoElimination:
                case VotingResult.Outcome.NoVotesCast:
                    FinishRound();
                    break;
            }

            return result;
        }

        private VotingResult TallyVotes()
        {
            var voteCounts = new Dictionary<string, int>();

            foreach (var targetId in _currentVotes.Values)
            {
                voteCounts.TryGetValue(targetId, out int current);
                voteCounts[targetId] = current + 1;
            }

            if (voteCounts.Count == 0)
            {
                return new VotingResult
                {
                    ResultType = VotingResult.Outcome.NoVotesCast,
                    VoteCounts = voteCounts
                };
            }

            int topCount = voteCounts.Values.Max();
            var topIds = voteCounts.Where(kv => kv.Value == topCount).Select(kv => kv.Key).ToList();

            if (topIds.Count == 1)
            {
                return new VotingResult
                {
                    ResultType = VotingResult.Outcome.PlayerEliminated,
                    EliminatedPlayer = GetPlayer(topIds[0]),
                    VoteCounts = voteCounts
                };
            }

            // Tie: decide whether to allow a re-vote or give up.
            var tiedPlayers = topIds.Select(GetPlayer).ToList();

            bool canRetry = _tiebreakerAttempts < MaxTiebreakerAttempts;

            return new VotingResult
            {
                ResultType = canRetry
                    ? VotingResult.Outcome.TieRequiresRevote
                    : VotingResult.Outcome.TieUnresolvedNoElimination,
                TiedCandidates = tiedPlayers,
                VoteCounts = voteCounts
            };
        }

        // --- Round wrap-up -------------------------------------------------

        private void FinishRound()
        {
            SetPhase(GamePhase.RoundResult);

            if (IsGameOver())
            {
                EndGame();
            }
            else
            {
                StartNextRound();
            }
        }

        // --- Win condition ---------------------------------------------------

        private bool IsGameOver()
        {
            return ActivePlayers().Count() <= _config.SurvivorsTarget;
        }

        private void EndGame()
        {
            SetPhase(GamePhase.GameOver);

            var survivors = ActivePlayers().ToList();

            var reason = survivors.Count > 0
                ? GameOverResult.Reason.SurvivorsTargetReached
                : GameOverResult.Reason.AllPlayersEliminated;

            var result = new GameOverResult
            {
                EndReason = reason,
                Survivors = survivors
            };

            OnGameOverResolved?.Invoke(result);
        }

        // --- Special Cards ---------------------------------------------------

        // General-purpose entry point for effects that don't need extra parameters
        // (VoteImmunity, RevealHiddenTrait, ForceRevealAll).
        public SpecialCardEffectResult UseSpecialCard(string casterPlayerId, string targetPlayerId)
        {
            var caster = GetPlayer(casterPlayerId);
            if (caster == null || caster.IsEliminated)
                return SpecialCardEffectResult.Fail("Invalid caster.");

            if (caster.HasUsedSpecialCard)
                return SpecialCardEffectResult.Fail("Special card already used.");

            if (caster.Special == null)
                return SpecialCardEffectResult.Fail("Player has no special card.");

            if (!_effectRegistry.TryGet(caster.Special.EffectType, out var effect))
                return SpecialCardEffectResult.Fail($"Unknown effect type: {caster.Special.EffectType}");

            var target = targetPlayerId != null ? GetPlayer(targetPlayerId) : null;

            if (!effect.CanApply(this, caster, target))
                return SpecialCardEffectResult.Fail("Effect cannot be applied in this context.");

            // Effect-specific logic that needs extra data lives here, keyed by
            // effect type, rather than bloating ISpecialCardEffect's signature
            // with parameters only some effects need.
            SpecialCardEffectResult result = caster.Special.EffectType switch
            {
                SpecialCardEffectType.RevealHiddenTrait => ApplyRevealHiddenTrait(caster, target),
                SpecialCardEffectType.VoteImmunity => ApplyVoteImmunity(effect, caster, target),
                SpecialCardEffectType.ForceRevealAll => ApplyForceRevealAll(caster, target),
                _ => SpecialCardEffectResult.Fail("Effect requires extra parameters, use the dedicated overload.")
            };

            if (result.Success)
            {
                caster.HasUsedSpecialCard = true;
                OnSpecialCardUsed?.Invoke(caster, caster.Special);
            }

            return result;
        }

        // Dedicated overload for SwapTrait, which needs to know which category to swap.
        public SpecialCardEffectResult UseSwapTraitSpecialCard(
            string casterPlayerId, string targetPlayerId, CardCategory category)
        {
            var caster = GetPlayer(casterPlayerId);
            var target = GetPlayer(targetPlayerId);

            if (caster == null || target == null || caster.IsEliminated || target.IsEliminated)
                return SpecialCardEffectResult.Fail("Invalid caster or target.");

            if (caster.HasUsedSpecialCard)
                return SpecialCardEffectResult.Fail("Special card already used.");

            if (caster.Special?.EffectType != SpecialCardEffectType.SwapTrait)
                return SpecialCardEffectResult.Fail("Player's special card is not a swap effect.");

            if (caster.IsCategoryRevealed(category) || target.IsCategoryRevealed(category))
                return SpecialCardEffectResult.Fail("Category must be hidden for both players.");

            var casterTrait = caster.GetTrait(category);
            var targetTrait = target.GetTrait(category);

            caster.ReplaceTrait(category, targetTrait);
            target.ReplaceTrait(category, casterTrait);

            caster.HasUsedSpecialCard = true;
            OnSpecialCardUsed?.Invoke(caster, caster.Special);

            return SpecialCardEffectResult.Ok();
        }

        private SpecialCardEffectResult ApplyRevealHiddenTrait(PlayerData caster, PlayerData target)
        {
            var hiddenTrait = target.Traits.FirstOrDefault(t => !target.IsCategoryRevealed(t.Category));
            if (hiddenTrait == null)
                return SpecialCardEffectResult.Fail("Target has no hidden traits.");

            // Note: this does NOT call target.RevealCategory() — the target's
            // card stays hidden from everyone else. Only the caster receives
            // this trait in the result, to be shown privately in their UI.
            return SpecialCardEffectResult.Ok(hiddenTrait);
        }

        private SpecialCardEffectResult ApplyVoteImmunity(ISpecialCardEffect effect, PlayerData caster, PlayerData target)
        {
            effect.Apply(this, caster, target);
            return SpecialCardEffectResult.Ok();
        }

        private SpecialCardEffectResult ApplyForceRevealAll(PlayerData caster, PlayerData target)
        {
            var hiddenTrait = target.Traits.FirstOrDefault(t => !target.IsCategoryRevealed(t.Category));
            if (hiddenTrait == null)
                return SpecialCardEffectResult.Fail("Target has no hidden traits.");

            target.RevealCategory(hiddenTrait.Category);
            OnTraitRevealed?.Invoke(target, hiddenTrait); // public — everyone sees it

            return SpecialCardEffectResult.Ok(hiddenTrait);
        }

        // --- Helpers ---------------------------------------------------------

        public IEnumerable<PlayerData> ActivePlayers()
        {
            return Players.Where(p => !p.IsEliminated);
        }

        public PlayerData GetPlayer(string playerId)
        {
            return Players.FirstOrDefault(p => p.PlayerId == playerId);
        }
    }
}