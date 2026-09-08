namespace Bunker.UI
{
    // Minimal service locator so screens can reach the active ILobbyService
    // without each one needing a manually wired reference in the Inspector.
    public static class LobbyServiceLocator
    {
        public static ILobbyService Current { get; set; }
    }
}
