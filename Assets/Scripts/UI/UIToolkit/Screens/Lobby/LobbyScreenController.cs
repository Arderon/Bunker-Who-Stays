using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class LobbyScreenController : ScreenController
    {
        private readonly Label _lobbyCodeValueLabel, _playersCountLabel, _survivorsTargetValueLabel,
            _startGameErrorLabel, _waitingForHostLabel;
        private readonly Button _copyCodeButton, _startGameButton, _leaveLobbyButton,
            _survivorsIncreaseButton, _survivorsDecreaseButton;
        private readonly VisualElement _survivorsTargetSelector;
        private readonly ListView _playersListView;
        private readonly VisualTreeAsset _playerItemAsset;

        private ILobbyService _lobby;
        private List<LobbyPlayerInfo> _players = new();

        public LobbyScreenController(VisualElement root, VisualTreeAsset playerItemAsset) : base(root)
        {
            _playerItemAsset = playerItemAsset;

            _lobbyCodeValueLabel = root.Q<Label>("lobby-code-value");
            _copyCodeButton = root.Q<Button>("copy-code-button");
            _playersCountLabel = root.Q<Label>("players-count-label");
            _playersListView = root.Q<ListView>("players-list");
            _survivorsTargetSelector = root.Q<VisualElement>("survivors-target-selector");
            _survivorsTargetValueLabel = root.Q<Label>("survivors-target-value");
            _survivorsIncreaseButton = root.Q<Button>("survivors-increase-button");
            _survivorsDecreaseButton = root.Q<Button>("survivors-decrease-button");
            _startGameButton = root.Q<Button>("start-game-button");
            _startGameErrorLabel = root.Q<Label>("start-game-error-label");
            _waitingForHostLabel = root.Q<Label>("waiting-for-host-label");
            _leaveLobbyButton = root.Q<Button>("leave-lobby-button");

            _playersListView.makeItem = () => _playerItemAsset.CloneTree();
            _playersListView.bindItem = (element, index) => BindPlayerItem(element, _players[index]);
            _playersListView.itemsSource = _players;

            _copyCodeButton.clicked += () => GUIUtility.systemCopyBuffer = _lobbyCodeValueLabel.text;
            _survivorsIncreaseButton.clicked += () => ChangeSurvivorsTarget(+1);
            _survivorsDecreaseButton.clicked += () => ChangeSurvivorsTarget(-1);
            _startGameButton.clicked += () => _lobby.StartGame();
            _leaveLobbyButton.clicked += OnLeaveClicked;

            RefreshStaticText();
        }

        private async void RefreshStaticText()
        {
            _copyCodeButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_lobby_copy_code");
            _startGameButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_lobby_start_game");
            _waitingForHostLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_lobby_waiting_for_host");
            _leaveLobbyButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_lobby_leave");
        }

        protected override void OnShown()
        {
            _lobby = LobbyServiceLocator.Current;
            _lobby.OnPlayerListChanged += OnPlayerListChanged;
            _lobby.OnStartValidationChanged += OnStartValidationChanged;
            _lobby.OnGameStarted += OnGameStarted;

            _lobbyCodeValueLabel.text = _lobby.LobbyCode;

            bool isHost = _lobby.IsLocalPlayerHost;
            _survivorsTargetSelector.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            _startGameButton.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            _waitingForHostLabel.style.display = isHost ? DisplayStyle.None : DisplayStyle.Flex;

            _survivorsTargetValueLabel.text = _lobby.SurvivorsTarget.ToString();
        }

        protected override void OnHidden()
        {
            if (_lobby == null) return;
            _lobby.OnPlayerListChanged -= OnPlayerListChanged;
            _lobby.OnStartValidationChanged -= OnStartValidationChanged;
            _lobby.OnGameStarted -= OnGameStarted;
        }

        private void ChangeSurvivorsTarget(int delta)
        {
            _lobby.SurvivorsTarget = Mathf.Max(1, _lobby.SurvivorsTarget + delta);
            _survivorsTargetValueLabel.text = _lobby.SurvivorsTarget.ToString();
        }

        private async void OnPlayerListChanged(List<LobbyPlayerInfo> players)
        {
            _players = players;
            _playersListView.itemsSource = _players;
            _playersListView.RefreshItems();

            _playersCountLabel.text = await LocalizedTextService.GetTextAsync(
                LocalizationTableNames.UI, "ui_lobby_players_count", players.Count, 12);
        }

        private async void BindPlayerItem(VisualElement element, LobbyPlayerInfo player)
        {
            element.Q<Label>("display-name-label").text = player.DisplayName;

            var hostTag = element.Q<Label>("host-tag");
            hostTag.style.display = player.IsHost ? DisplayStyle.Flex : DisplayStyle.None;

            var key = player.IsReady ? "ui_lobby_player_ready" : "ui_lobby_player_not_ready";
            element.Q<Label>("ready-status-label").text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
        }

        private void OnStartValidationChanged(GameStartValidationResult validation)
        {
            _startGameButton.SetEnabled(validation.CanStart);
            _startGameErrorLabel.style.display = validation.CanStart ? DisplayStyle.None : DisplayStyle.Flex;
            if (!validation.CanStart) _startGameErrorLabel.text = validation.FailReason;
        }

        private void OnGameStarted()
        {
            var gameScreen = UIManager.Instance.ShowScreen<GameScreenController>();
            gameScreen.Bind(_lobby.CurrentSession);
        }

        private void OnLeaveClicked()
        {
            _lobby.LeaveLobby();
            UIManager.Instance.ShowScreen<MainMenuScreenController>();
        }
    }
}