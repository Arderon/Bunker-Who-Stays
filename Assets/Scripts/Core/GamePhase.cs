namespace Bunker.Core
{
    // High-level phase of a single game session.
    // UI and networking layers react to phase changes; they never drive them directly.
    public enum GamePhase
    {
        Lobby,
        Dealing,
        Reveal,
        Discussion,
        Voting,
        VotingTiebreaker,   // re-vote among tied candidates only
        RoundResult,
        GameOver
    }
}