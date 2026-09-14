using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class JoinLobbyScreen : UIScreen
    {
        [SerializeField] private TMP_InputField _codeInputField;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _errorLabel;

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
            if (_lobby == null) return;
            _lobby.OnJoinFailed -= OnJoinFailed;
            _lobby.OnPlayerListChanged -= OnJoinedLobby;
        }

        private void OnJoinClicked()
        {
            var code = _codeInputField.text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return;

            // JoinLobby is fire-and-forget (async void on the Colyseus-backed
            // service) — the only signal that it actually succeeded is its
            // first OnPlayerListChanged, which fires once the room is fully
            // joined and synced. Guard against stacking handlers if the user
            // retries after a failed attempt.
            _lobby.OnPlayerListChanged -= OnJoinedLobby;
            _lobby.OnPlayerListChanged += OnJoinedLobby;

            _lobby.JoinLobby(code, PlayerPrefsNames.GetLocalDisplayName());
        }

        private void OnJoinedLobby(List<LobbyPlayerInfo> players)
        {
            _lobby.OnPlayerListChanged -= OnJoinedLobby;
            UIManager.Instance.ShowScreen<LobbyScreen>();
        }

        private void OnJoinFailed(string localizationKey)
        {
            _lobby.OnPlayerListChanged -= OnJoinedLobby;

            StartCoroutine(Bunker.Localization.LocalizedTextService.GetTextCoroutine(
                Bunker.Localization.LocalizationTableNames.UI,
                localizationKey,
                text =>
                {
                    _errorLabel.text = text;
                    _errorLabel.gameObject.SetActive(true);
                }));
        }
    }
}