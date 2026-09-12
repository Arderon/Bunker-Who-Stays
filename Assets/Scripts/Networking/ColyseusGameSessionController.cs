using System;
using System.Collections.Generic;
using System.Linq;
using Colyseus;
using Colyseus.Schema;
using Bunker.Core;

namespace Bunker.Networking
{
    // Colyseus-backed implementation of IGameSessionView. Trait content
    // (id/localizationKey) does NOT come through TraitSchema — verified
    // against a live server that @view()-gated fields on Schema instances
    // nested inside an ArraySchema never reliably reach even the owning
    // client. TraitSchema only carries category/revealed (plain public
    // fields, synced normally); real content arrives via three message
    // types instead: "dealtHand" (private, once, owner's full hand),
    // "traitRevealed" (broadcast, whenever any trait is revealed), and
    // "traitUpdated" (private, when SwapTrait changes hidden content).
    //
    // State callbacks go through Colyseus.Schema.Callbacks (schema 3.x):
    // Schema / ArraySchema / MapSchema instances no longer expose OnChange,
    // OnAdd or OnRemove methods of their own.
    public class ColyseusGameSessionController : IGameSessionView
    {
        private readonly ColyseusRoom<GameStateSchema> room;
        private readonly StateCallbackStrategy<GameStateSchema> callbacks;
        private readonly Dictionary<string, PlayerData> localPlayers = new();
        private readonly string localPlayerId;

        public GamePhase Phase { get; private set; }
        public int CurrentRound { get; private set; }
        public List<PlayerData> Players => localPlayers.Values.ToList();
        public string CurrentTurnPlayerId { get; private set; }

        public event Action<GamePhase> OnPhaseChanged;
        public event Action<int> OnRoundStarted;
        public event Action<PlayerData, CharacterTrait> OnTraitRevealed;
        public event Action OnRevealPassCompleted;
        public event Action<PlayerData, SpecialCard> OnSpecialCardUsed;
        public event Action<VotingResult> OnVotingResolved;
        public event Action<PlayerData> OnPlayerEliminated;
        public event Action<GameOverResult> OnGameOverResolved;
        public event Action<string> OnActionRejected;

        // Not part of IGameSessionView — UI that cares (e.g. a "peek result"
        // popup) subscribes to this directly.
        public event Action<CharacterTrait> OnPrivateTraitPeeked;

        public ColyseusGameSessionController(ColyseusRoom<GameStateSchema> room, string localPlayerId)
        {
            this.room = room;
            this.localPlayerId = localPlayerId;
            callbacks = Callbacks.Get(room);
            WireStateCallbacks();
            WireMessageCallbacks();
        }

        // --- State synchronization (structure/phase only — no trait content) ---

        private void WireStateCallbacks()
        {
            callbacks.OnChange(room.State, () =>
            {
                var newPhase = (GamePhase)room.State.phase;
                if (newPhase != Phase)
                {
                    Phase = newPhase;
                    OnPhaseChanged?.Invoke(Phase);
                }

                if (room.State.currentRound != CurrentRound)
                {
                    CurrentRound = room.State.currentRound;
                    OnRoundStarted?.Invoke(CurrentRound);
                }

                CurrentTurnPlayerId = room.State.currentTurnPlayerId;
            });

            callbacks.OnAdd(state => state.players, (string key, PlayerSchema netPlayer) =>
            {
                localPlayers[key] = MapPlayer(netPlayer);
                WirePlayerCallbacks(key, netPlayer);
            });

            callbacks.OnRemove(state => state.players, (string key, PlayerSchema removed) =>
            {
                localPlayers.Remove(key);
            });
        }

        private void WirePlayerCallbacks(string playerId, PlayerSchema netPlayer)
        {
            callbacks.OnChange(netPlayer, () =>
            {
                if (!localPlayers.TryGetValue(playerId, out var localPlayer)) return;

                bool wasEliminated = localPlayer.IsEliminated;
                bool hadUsedSpecial = localPlayer.HasUsedSpecialCard;

                localPlayer.DisplayName = netPlayer.displayName;
                localPlayer.IsEliminated = netPlayer.isEliminated;
                localPlayer.HasUsedSpecialCard = netPlayer.hasUsedSpecialCard;

                if (netPlayer.isEliminated && !wasEliminated)
                    OnPlayerEliminated?.Invoke(localPlayer);

                if (netPlayer.hasUsedSpecialCard && !hadUsedSpecial && localPlayer.Special != null)
                    OnSpecialCardUsed?.Invoke(localPlayer, localPlayer.Special);
            });

            // special is @view()-gated and DOES work correctly for direct
            // field references (confirmed against a live server) — unlike
            // trait content, this one behaves as originally designed.
            callbacks.Listen(netPlayer, p => p.special, (SpecialCardSchema current, SpecialCardSchema previous) =>
            {
                if (current == null) return;
                if (!localPlayers.TryGetValue(playerId, out var localPlayer)) return;
                localPlayer.Special = MapSpecialCard(current);
            });

            // immediate (the default) also replays traits already present on
            // the array, so no separate initial loop is needed here.
            callbacks.OnAdd(netPlayer, p => p.traits, (int index, TraitSchema trait) =>
                WireTraitCallback(playerId, trait));
        }

        // TraitSchema only carries category/revealed — this callback keeps
        // the local "revealed" bookkeeping in sync promptly, but
        // deliberately does NOT fire OnTraitRevealed here, since it has no
        // real trait content to hand the UI. That event fires only from
        // the "traitRevealed" message handler below, which carries both
        // the flag and the content together.
        private void WireTraitCallback(string playerId, TraitSchema netTrait)
        {
            callbacks.OnChange(netTrait, () =>
            {
                if (!localPlayers.TryGetValue(playerId, out var localPlayer)) return;

                var category = (CardCategory)netTrait.category;

                // The traits array can arrive after the player was mapped, so
                // make sure a placeholder exists for this category before the
                // content-carrying messages land.
                if (localPlayer.GetTrait(category) == null)
                    localPlayer.ReplaceOrAddTrait(category, new CharacterTrait(id: "", category, localizationKey: ""));

                if (netTrait.revealed && !localPlayer.IsCategoryRevealed(category))
                {
                    localPlayer.RevealCategory(category);
                }
            });
        }

        private PlayerData MapPlayer(PlayerSchema netPlayer)
        {
            var player = new PlayerData(netPlayer.playerId, netPlayer.displayName)
            {
                IsEliminated = netPlayer.isEliminated,
                HasUsedSpecialCard = netPlayer.hasUsedSpecialCard
            };

            // Placeholder traits with empty content — real id/localizationKey
            // arrive later via dealtHand (own hand) or traitRevealed
            // (once any player's trait becomes public).
            var traits = new List<CharacterTrait>();
            if (netPlayer.traits != null)
            {
                // ArraySchema<T> is not enumerable — index into it explicitly.
                for (int i = 0; i < netPlayer.traits.Count; i++)
                {
                    var netTrait = netPlayer.traits[i];
                    if (netTrait == null) continue;

                    var category = (CardCategory)netTrait.category;
                    traits.Add(new CharacterTrait(id: "", category, localizationKey: ""));
                    if (netTrait.revealed) player.RevealCategory(category);
                }
            }
            player.AssignTraits(traits);

            if (netPlayer.special != null)
                player.Special = MapSpecialCard(netPlayer.special);

            return player;
        }

        private SpecialCard MapSpecialCard(SpecialCardSchema netCard) =>
            new SpecialCard(netCard.id, netCard.effectType, netCard.localizationKey);

        // --- Server -> client messages ------------------------------------

        private void WireMessageCallbacks()
        {
            room.OnMessage<ActionRejectedMessage>("actionRejected", (msg) => OnActionRejected?.Invoke(msg?.key));

            // Private: the local player's own full hand, sent once right
            // after dealing. Populates real content for all 7 categories
            // regardless of revealed state.
            room.OnMessage<DealtHandMessage>("dealtHand", (msg) =>
            {
                if (msg?.traits == null) return;
                if (!localPlayers.TryGetValue(localPlayerId, out var localPlayer)) return;

                foreach (var traitMsg in msg.traits)
                {
                    var trait = new CharacterTrait(traitMsg.id, (CardCategory)traitMsg.category, traitMsg.localizationKey);
                    localPlayer.ReplaceOrAddTrait(trait.Category, trait);
                }
            });

            // Broadcast: any player's trait becoming publicly revealed.
            // This is the sole source of truth for both the content AND
            // the moment OnTraitRevealed fires for the UI.
            room.OnMessage<TraitRevealedMessage>("traitRevealed", (msg) =>
            {
                if (msg == null) return;
                if (!localPlayers.TryGetValue(msg.playerId, out var player)) return;

                var category = (CardCategory)msg.category;
                var trait = new CharacterTrait(msg.id, category, msg.localizationKey);
                player.ReplaceOrAddTrait(category, trait);
                player.RevealCategory(category);

                OnTraitRevealed?.Invoke(player, trait);
            });

            // Private: SwapTrait changed one of the local player's own
            // hidden traits without revealing it — update content only,
            // no reveal, no OnTraitRevealed.
            room.OnMessage<TraitUpdatedMessage>("traitUpdated", (msg) =>
            {
                if (msg == null) return;
                if (!localPlayers.TryGetValue(localPlayerId, out var localPlayer)) return;

                var category = (CardCategory)msg.category;
                var trait = new CharacterTrait(msg.id, category, msg.localizationKey);
                localPlayer.ReplaceOrAddTrait(category, trait);
            });

            room.OnMessage<object>("revealPassCompleted", (ignored) => OnRevealPassCompleted?.Invoke());

            room.OnMessage<VotingResolvedMessage>("votingResolved", (msg) =>
            {
                if (msg == null) return;

                var result = new VotingResult
                {
                    ResultType = (VotingResult.Outcome)msg.resultType,
                    EliminatedPlayer = string.IsNullOrEmpty(msg.eliminatedPlayerId)
                        ? null
                        : GetPlayer(msg.eliminatedPlayerId),
                    TiedCandidates = msg.tiedCandidateIds == null
                        ? new List<PlayerData>()
                        : msg.tiedCandidateIds.Select(GetPlayer).Where(p => p != null).ToList(),
                    VoteCounts = msg.voteCounts ?? new Dictionary<string, int>()
                };
                OnVotingResolved?.Invoke(result);
            });

            room.OnMessage<GameOverMessage>("gameOver", (msg) =>
            {
                if (msg == null) return;

                OnGameOverResolved?.Invoke(new GameOverResult
                {
                    EndReason = (GameOverResult.Reason)msg.endReason,
                    Survivors = msg.survivorPlayerIds == null
                        ? new List<PlayerData>()
                        : msg.survivorPlayerIds.Select(GetPlayer).Where(p => p != null).ToList()
                });
            });

            // RevealHiddenTrait's private payload.
            room.OnMessage<PrivateTraitPeekMessage>("privateTraitPeek", (msg) =>
            {
                if (msg == null) return;

                var trait = new CharacterTrait(msg.id, (CardCategory)msg.category, msg.localizationKey);
                OnPrivateTraitPeeked?.Invoke(trait);
            });
        }

        // --- IGameSessionView: read accessors ---------------------------------

        public IEnumerable<PlayerData> ActivePlayers() => localPlayers.Values.Where(p => !p.IsEliminated);
        public PlayerData GetPlayer(string playerId) => localPlayers.TryGetValue(playerId, out var p) ? p : null;

        // --- IGameSessionView: actions (optimistic — see interface note) ---

        public bool RevealNextTrait(string playerId, CardCategory category)
        {
            _ = room.Send("revealTrait", new { category = (int)category });
            return true;
        }

        public bool CastVote(string voterPlayerId, string targetPlayerId)
        {
            _ = room.Send("castVote", new { targetPlayerId });
            return true;
        }

        public VotingResult ResolveVotes()
        {
            _ = room.Send("resolveVotes", new { });
            return null; // actual result arrives asynchronously via OnVotingResolved
        }

        public void StartDiscussionPhase(int durationSeconds)
        {
            _ = room.Send("startDiscussionPhase", new { durationSeconds });
        }

        public void StartVotingPhase()
        {
            _ = room.Send("skipDiscussion", new { });
        }

        public SpecialCardEffectResult UseSpecialCard(string casterPlayerId, string targetPlayerId)
        {
            _ = room.Send("useSpecialCard", new { targetPlayerId });
            return SpecialCardEffectResult.Ok();
        }

        public SpecialCardEffectResult UseSwapTraitSpecialCard(string casterPlayerId, string targetPlayerId, CardCategory category)
        {
            _ = room.Send("useSwapTraitSpecialCard", new { targetPlayerId, category = (int)category });
            return SpecialCardEffectResult.Ok();
        }
    }
}
