using System;
using System.Collections.Generic;
using Bunker.Core;

namespace Bunker.UI
{
    public struct LobbyPlayerInfo
    {
        public string PlayerId;
        public string DisplayName;
        public bool IsHost;
        public bool IsReady;
    }

    // Abstraction over "however players get into a lobby". Currently backed
    // by ColyseusLobbyService (full switch to Colyseus's built-in
    // matchmaking, per the stage-0 decision — no UGS Lobby involved).
    public interface ILobbyService
    {
        event Action<List<LobbyPlayerInfo>> OnPlayerListChanged;
        event Action<GameStartValidationResult> OnStartValidationChanged;
        event Action OnGameStarted;
        event Action<string> OnJoinFailed; // localization key describing the failure

        string LobbyCode { get; }
        bool IsLocalPlayerHost { get; }
        int SurvivorsTarget { get; set; }

        void CreateLobby(string hostDisplayName);
        void JoinLobby(string code, string displayName);
        void SetLocalPlayerReady(bool ready);
        void LeaveLobby();
        void StartGame();

        // Exposes the underlying session once the game actually starts, so
        // GameScreen can subscribe to it. Typed as the interface (not the
        // concrete offline GameSession class) since network-backed
        // implementations (Colyseus) never subclass GameSession — they
        // implement IGameSessionView independently.
        IGameSessionView CurrentSession { get; }
    }
}
