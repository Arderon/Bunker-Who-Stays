using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class JoinLobbyScreen : UIScreen
    {
        [SerializeField] private InputField _codeInputField;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Text _errorLabel;

        private ILobbyService _lobby;

        private void Awake()
        {
            _joinButton.onClick.AddListener(OnJoinClicked);
            _cancelButton.onClick.AddListener(() => UIManager.Instance.ShowScreen<MainMenuScreen>());
        }

        protected override void OnShown()
        {
            _codeInputField.text = string.Empty;
            _errorLabel.gameObject.SetActive(false);

            _lobby = LobbyServiceLocator.Current;
            _lobby.OnJoinFailed += OnJoinFailed;
        }

        protected override void OnHidden()
        {
            if (_lobby != null) _lobby.OnJoinFailed -= OnJoinFailed;
        }

        private void OnJoinClicked()
        {
            var code = _codeInputField.text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return;

            _lobby.JoinLobby(code, PlayerPrefsNames.GetLocalDisplayName());
        }

        private void OnJoinFailed(string localizationKey)
        {
            StartCoroutine(Bunker.Localization.LocalizedTextService.GetTextCoroutine(
                Bunker.Localization.LocalizationTableNames.UI,
                localizationKey,
                text =>
                {
                    _errorLabel.text = text;
                    _errorLabel.gameObject.SetActive(true);
                }));
        }

        // On successful join, LobbyScreen is shown from wherever OnPlayerListChanged
        // first fires (see LobbyScreen.OnShown / a bootstrap subscriber) —
        // kept out of this screen to avoid every join-flow variant duplicating navigation logic.
    }
}