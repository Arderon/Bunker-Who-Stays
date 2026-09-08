using System;
using System.Collections.Generic;

namespace Bunker.Core
{
    // Read/action surface shared by the local (offline/hotseat) GameSession
    // and the network-backed client proxy (ColyseusGameSessionController).
    // UI panels bind against this interface, not a concrete class, so the
    // same UI code works whether driven locally or over the network.
    //
    // Important: RevealNextTrait/CastVote/UseSpecialCard/UseSwapTraitSpecialCard
    // return a value synchronously for the LOCAL implementation, but the
    // NETWORKED implementation cannot know the server's answer synchronously —
    // it returns an optimistic "request sent" result immediately, and reports
    // actual rejection later via OnActionRejected. Callers that need to react
    // to rejection should subscribe to OnActionRejected rather than relying
    // solely on the return value.
    public interface IGameSessionView
    {
        GamePhase Phase { get; }
        int CurrentRound { get; }
        List<PlayerData> Players { get; }
        string CurrentTurnPlayerId { get; }

        event Action<GamePhase> OnPhaseChanged;
        event Action<int> OnRoundStarted;
        event Action<PlayerData, CharacterTrait> OnTraitRevealed;
        event Action OnRevealPassCompleted;
        event Action<PlayerData, SpecialCard> OnSpecialCardUsed;
        event Action<VotingResult> OnVotingResolved;
        event Action<PlayerData> OnPlayerEliminated;
        event Action<GameOverResult> OnGameOverResolved;
        event Action<string> OnActionRejected; // localization key describing the rejection reason

        IEnumerable<PlayerData> ActivePlayers();
        PlayerData GetPlayer(string playerId);

        bool RevealNextTrait(string playerId, CardCategory category);
        bool CastVote(string voterPlayerId, string targetPlayerId);
        VotingResult ResolveVotes();
        void StartDiscussionPhase(int durationSeconds);
        void StartVotingPhase();
        SpecialCardEffectResult UseSpecialCard(string casterPlayerId, string targetPlayerId);
        SpecialCardEffectResult UseSwapTraitSpecialCard(string casterPlayerId, string targetPlayerId, CardCategory category);
    }
}
