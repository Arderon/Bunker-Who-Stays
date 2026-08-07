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
        RoundResult,
        GameOver
    }
}