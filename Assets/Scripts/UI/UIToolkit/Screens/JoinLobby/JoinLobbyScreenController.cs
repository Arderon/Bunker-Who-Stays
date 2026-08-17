using UnityEngine.UIElements;
using Bunker.Localization;

namespace Bunker.UI
{
    public class JoinLobbyScreenController : ScreenController
    {
        private readonly TextField _codeInput;
        private readonly Button _joinButton, _cancelButton;
        private readonly Label _errorLabel;
        private ILobbyService _lobby;

        public JoinLobbyScreenController(VisualElement root) : base(root)
        {
            _codeInput = root.Q<TextField>("code-input");
            _joinButton = root.Q<Button>("join-button");
            _cancelButton = root.Q<Button>("cancel-button");
            _errorLabel = root.Q<Label>("error-label");

            _joinButton.clicked += OnJoinClicked;
            _cancelButton.clicked += () => UIManager.Instance.ShowScreen<MainMenuScreenController>();

            RefreshLocalizedText();
        }

        private async void RefreshLocalizedText()
        {
            _joinButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_join_button");
            _cancelButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_join_cancel");
            _codeInput.textEdition.placeholder = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_join_code_placeholder");
        }

        protected override void OnShown()
        {
            _codeInput.value = string.Empty;
            _errorLabel.AddToClassList("hidden");
            _lobby = LobbyServiceLocator.Current;
            _lobby.OnJoinFailed += OnJoinFailed;
        }

        protected override void OnHidden()
        {
            if (_lobby != null) _lobby.OnJoinFailed -= OnJoinFailed;
        }

        private void OnJoinClicked()
        {
            var code = _codeInput.value.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(code))
                _lobby.JoinLobby(code, PlayerPrefsNames.GetLocalDisplayName());
        }

        private async void OnJoinFailed(string key)
        {
            _errorLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
            _errorLabel.RemoveFromClassList("hidden");
        }
    }
}