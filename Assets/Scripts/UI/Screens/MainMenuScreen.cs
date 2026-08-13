using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class MainMenuScreen : UIScreen
    {
        [SerializeField] private Button _createLobbyButton;
        [SerializeField] private Button _joinLobbyButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;

        private void Awake()
        {
            _createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
            _joinLobbyButton.onClick.AddListener(() => UIManager.Instance.ShowScreen<JoinLobbyScreen>());
            _settingsButton.onClick.AddListener(() => UIManager.Instance.ShowScreen<SettingsScreen>());
            _exitButton.onClick.AddListener(() => Application.Quit());
        }

        private void OnCreateLobbyClicked()
        {
            var lobby = LobbyServiceLocator.Current;
            lobby.CreateLobby(displayName: PlayerPrefsNames.GetLocalDisplayName());
            UIManager.Instance.ShowScreen<LobbyScreen>();
        }
    }
}