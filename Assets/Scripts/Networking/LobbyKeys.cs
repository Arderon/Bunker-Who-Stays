namespace Bunker.Networking
{
    // Names of custom fields stored in UGS Lobby/Player data.
    // Centralized here so they're never duplicated as raw strings.
    public static class LobbyDataKeys
    {
        public const string SurvivorsTarget = "SurvivorsTarget";
        public const string GameStarted = "GameStarted";
    }

    public static class PlayerDataKeys
    {
        public const string DisplayName = "DisplayName";
        public const string IsReady = "IsReady";
    }
}