namespace Bunker.UI
{
    // Minimal service locator so screens can reach the active ILobbyService
    // without each one needing a manually wired reference in the Inspector.
    // Swapped from LocalLobbyService to a UGS-backed implementation in stage 4
    // by changing a single assignment at bootstrap — no screen script changes.
    public static class LobbyServiceLocator
    {
        public static ILobbyService Current { get; set; }
    }
}