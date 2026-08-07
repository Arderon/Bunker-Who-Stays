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

        // --- Lifecycle -----------------------------------------------------

        public void StartGame()
        {
            if (Phase != GamePhase.Lobby)
            {
                Debug.LogWarning("[GameSession] StartGame called outside of Lobby phase, ignoring.");
                return;
            }

            if (Players.Count <= _config.SurvivorsTarget)
            {
                Debug.LogError("[GameSession] Not enough players to run a game with this SurvivorsTarget.");
                return;
            }

            SetPhase(GamePhase.Dealing);
            _cardGenerator.DealToPlayers(Players);
            _turnOrder.Initialize(Players);

            CurrentRound = 0;
            StartNextRound();
        }

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

        // --- Voting (minimal skeleton, full rules land in section 1.6) -----

        private readonly Dictionary<string, string> _currentVotes = new(); // voterId -> targetId

        public void CastVote(string voterPlayerId, string targetPlayerId)
        {
            if (Phase != GamePhase.Voting)
            {
                Debug.LogWarning("[GameSession] Vote cast outside of Voting phase, ignored.");
                return;
            }

            _currentVotes[voterPlayerId] = targetPlayerId;
        }

        public void ResolveRound()
        {
            var eliminated = TallyVotesAndGetEliminated();

            if (eliminated != null)
            {
                eliminated.IsEliminated = true;
                OnPlayerEliminated?.Invoke(eliminated);
            }

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

        private PlayerData TallyVotesAndGetEliminated()
        {
            if (_currentVotes.Count == 0) return null;

            var counts = _currentVotes.Values
                .GroupBy(targetId => targetId)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            int topCount = counts[0].Count;
            var topCandidates = counts.Where(c => c.Count == topCount).ToList();

            if (topCandidates.Count > 1 && !_config.AllowVoteTies)
            {
                // Tie handling is intentionally left as a hook for section 1.6.
                // For now, no one is eliminated on a tie.
                Debug.Log("[GameSession] Vote tie detected, no elimination this round.");
                return null;
            }

            var eliminatedId = topCandidates[0].PlayerId;
            return Players.FirstOrDefault(p => p.PlayerId == eliminatedId);
        }

        // --- Win condition ---------------------------------------------------

        private bool IsGameOver()
        {
            return ActivePlayers().Count() <= _config.SurvivorsTarget;
        }

        private void EndGame()
        {
            SetPhase(GamePhase.GameOver);
            OnGameOver?.Invoke(ActivePlayers().ToList());
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