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

    // Abstraction over "however players get into a lobby". A local, single-device
    // implementation backs this during UI development (see LocalLobbyService).
    // In stage 4/5 this gets replaced by a UGS Lobby + Relay backed implementation
    // with the exact same interface, so no UI script needs to change.
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

        // Exposes the underlying session once the game actually starts,
        // so GameScreen can subscribe to it.
        GameSession CurrentSession { get; }
    }
}