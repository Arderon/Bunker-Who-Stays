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

        // Who is sitting in front of this screen. The UI cannot derive it —
        // Players is in server insertion order, so the first entry is the host,
        // not necessarily the viewer. Anything that decides "is this mine"
        // (own trait card, reveal buttons, the "your turn" call) must read this.
        // For hot-seat play the local player is whoever's turn it is.
        string LocalPlayerId { get; }

        event Action<GamePhase> OnPhaseChanged;
        event Action<int> OnRoundStarted;

        // Fires whenever the turn moves to a different player, including moves
        // the client did not cause (another player's reveal, a server-side
        // timeout, a player leaving). Subscribers must not infer turn changes
        // from OnTraitRevealed: the state patch carrying the new turn and the
        // message carrying the revealed trait arrive in the same packet with no
        // guaranteed order between them.
        event Action<string> OnTurnChanged;
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
