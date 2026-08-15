using UnityEngine;
using UnityEngine.UIElements;
using Bunker.Localization;

namespace Bunker.UI
{
    public class MainMenuScreenController : ScreenController
    {
        private readonly Label _titleLabel;
        private readonly Button _createLobbyButton, _joinLobbyButton, _settingsButton, _exitButton;

        public MainMenuScreenController(VisualElement root) : base(root)
        {
            _titleLabel = root.Q<Label>("title-label");
            _createLobbyButton = root.Q<Button>("create-lobby-button");
            _joinLobbyButton = root.Q<Button>("join-lobby-button");
            _settingsButton = root.Q<Button>("settings-button");
            _exitButton = root.Q<Button>("exit-button");

            _createLobbyButton.clicked += OnCreateLobbyClicked;
            _joinLobbyButton.clicked += () => UIManager.Instance.ShowScreen<JoinLobbyScreenController>();
            _settingsButton.clicked += () => UIManager.Instance.ShowScreen<SettingsScreenController>();
            _exitButton.clicked += Application.Quit;

            RefreshLocalizedText();
        }

        private async void RefreshLocalizedText()
        {
            _titleLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_menu_title");
            _createLobbyButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_menu_create_lobby");
            _joinLobbyButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_menu_join_lobby");
            _settingsButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_menu_settings");
            _exitButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_menu_exit");
        }

        private void OnCreateLobbyClicked()
        {
            LobbyServiceLocator.Current.CreateLobby(PlayerPrefsNames.GetLocalDisplayName());
            UIManager.Instance.ShowScreen<LobbyScreenController>();
        }
    }
}