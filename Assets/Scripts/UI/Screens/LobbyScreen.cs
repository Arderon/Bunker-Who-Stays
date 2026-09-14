using Bunker.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class LobbyScreen : UIScreen
    {
        [SerializeField] private TMP_Text _lobbyCodeValueLabel;
        [SerializeField] private Button _copyCodeButton;
        [SerializeField] private TMP_Text _playersCountLabel;
        [SerializeField] private Transform _playerListContainer;
        [SerializeField] private PlayerListItem _playerListItemPrefab;
        [SerializeField] private GameObject _survivorsTargetSelector;
        [SerializeField] private TMP_Text _survivorsTargetValueLabel;
        [SerializeField] private Button _survivorsTargetIncreaseButton;
        [SerializeField] private Button _survivorsTargetDecreaseButton;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TMP_Text _startGameErrorLabel;
        [SerializeField] private TMP_Text _waitingForHostLabel;
        [SerializeField] private Button _leaveLobbyButton;

        private ILobbyService _lobby;
        private readonly List<PlayerListItem> _spawnedItems = new();

        private void Awake()
        {
            _copyCodeButton.onClick.AddListener(() =>
                GUIUtility.systemCopyBuffer = _lobbyCodeValueLabel.text);

            _survivorsTargetIncreaseButton.onClick.AddListener(() => ChangeSurvivorsTarget(+1));
            _survivorsTargetDecreaseButton.onClick.AddListener(() => ChangeSurvivorsTarget(-1));

            _startGameButton.onClick.AddListener(() => _lobby.StartGame());
            _leaveLobbyButton.onClick.AddListener(OnLeaveClicked);
        }

        protected override void OnShown()
        {
            _lobby = LobbyServiceLocator.Current;
            _lobby.OnPlayerListChanged += OnPlayerListChanged;
            _lobby.OnStartValidationChanged += OnStartValidationChanged;
            _lobby.OnGameStarted += OnGameStarted;

            _lobbyCodeValueLabel.text = _lobby.LobbyCode;
            _survivorsTargetValueLabel.text = _lobby.SurvivorsTarget.ToString();

            // Subscribing above only catches list changes from this point on —
            // if the roster last changed before this screen subscribed (e.g. a
            // joining client's OnPlayerListChanged fires and navigates here
            // synchronously, before this handler is attached), the event is
            // missed entirely and the list would stay empty. Render the
            // current snapshot immediately as well.
            OnPlayerListChanged(_lobby.CurrentPlayers);
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

        private void OnPlayerListChanged(List<LobbyPlayerInfo> players)
        {
            // CreateLobby/JoinLobby are fire-and-forget on the caller's side
            // (MainMenuScreen/JoinLobbyScreen navigate here immediately,
            // without awaiting them) — for the Colyseus-backed service,
            // LobbyCode/IsLocalPlayerHost are still unresolved at OnShown()
            // time (room/room.State.players don't exist yet), since they're
            // only meaningful once the async network round-trip resolves.
            // OnPlayerListChanged always fires after that (end of
            // WireRoomCallbacks), so refresh both here too rather than
            // relying solely on OnShown()'s one-time read.
            _lobbyCodeValueLabel.text = _lobby.LobbyCode;

            bool isHost = _lobby.IsLocalPlayerHost;
            _survivorsTargetSelector.SetActive(isHost);
            _startGameButton.gameObject.SetActive(isHost);
            _waitingForHostLabel.gameObject.SetActive(!isHost);

            StartCoroutine(Bunker.Localization.LocalizedTextService.GetTextCoroutine(
                Bunker.Localization.LocalizationTableNames.UI,
                "ui_lobby_players_count",
                text => _playersCountLabel.text = text,
                players.Count, 12 /* max players, adjust as needed */));

            foreach (var item in _spawnedItems) Destroy(item.gameObject);
            _spawnedItems.Clear();

            foreach (var player in players)
            {
                var item = Instantiate(_playerListItemPrefab, _playerListContainer);
                item.Bind(player);
                _spawnedItems.Add(item);
            }
        }

        private void OnStartValidationChanged(GameStartValidationResult validation)
        {
            _startGameButton.interactable = validation.CanStart;
            _startGameErrorLabel.gameObject.SetActive(!validation.CanStart);

            if (!validation.CanStart)
            {
                // FailReason is currently a raw debug string (section 1.7);
                // showing it directly here as a placeholder until each failure
                // case is mapped to one of the ui_error_* localization keys.
                _startGameErrorLabel.text = validation.FailReason;
            }
        }

        private void OnGameStarted()
        {
            // Returns false unless a GameScreenV2Host is present in the scene,
            // so without it this path is exactly what it was before.
            if (GameV2.GameScreenV2Host.TryShowGame(_lobby.CurrentSession)) return;

            var gameScreen = UIManager.Instance.ShowScreen<GameScreen>();
            gameScreen.Bind(_lobby.CurrentSession);
        }

        private void OnLeaveClicked()
        {
            _lobby.LeaveLobby();
            UIManager.Instance.ShowScreen<MainMenuScreen>();
        }
    }
}