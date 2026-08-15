using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bunker.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Screens")]
        [SerializeField] private VisualTreeAsset _mainMenuAsset;
        [SerializeField] private VisualTreeAsset _joinLobbyAsset;
        [SerializeField] private VisualTreeAsset _lobbyAsset;
        [SerializeField] private VisualTreeAsset _gameAsset;
        [SerializeField] private VisualTreeAsset _gameOverAsset;
        [SerializeField] private VisualTreeAsset _settingsAsset;
        [SerializeField] private VisualTreeAsset _globalOverlayAsset;

        [Header("List Item Templates")]
        [SerializeField] private VisualTreeAsset _playerListItemAsset;
        [SerializeField] private VisualTreeAsset _voteTargetItemAsset;
        [SerializeField] private VisualTreeAsset _playerStatusIconAsset;
        [SerializeField] private VisualTreeAsset _traitSlotAsset;

        [Header("Game Screen Sub-Panels")]
        [SerializeField] private VisualTreeAsset _topBarAsset;
        [SerializeField] private VisualTreeAsset _myCardPanelAsset;
        [SerializeField] private VisualTreeAsset _revealPanelAsset;
        [SerializeField] private VisualTreeAsset _discussionPanelAsset;
        [SerializeField] private VisualTreeAsset _votingPanelAsset;
        [SerializeField] private VisualTreeAsset _roundResultPanelAsset;
        [SerializeField] private VisualTreeAsset _specialCardModalAsset;

        private VisualElement _screenLayer;
        private VisualElement _overlayLayer;
        private Dictionary<Type, ScreenController> _screensByType;
        private ScreenController _currentScreen;

        public GlobalOverlayController Overlay { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var root = GetComponent<UIDocument>().rootVisualElement;

            _screenLayer = new VisualElement { name = "screen-layer" };
            _screenLayer.style.flexGrow = 1;
            root.Add(_screenLayer);

            _overlayLayer = new VisualElement { name = "overlay-layer" };
            _overlayLayer.style.position = Position.Absolute;
            _overlayLayer.pickingMode = PickingMode.Ignore;
            _overlayLayer.StretchToParentSize();
            root.Add(_overlayLayer);

            var mainMenu = new MainMenuScreenController(Spawn(_mainMenuAsset));
            var joinLobby = new JoinLobbyScreenController(Spawn(_joinLobbyAsset));
            var lobby = new LobbyScreenController(Spawn(_lobbyAsset), _playerListItemAsset);
            var game = new GameScreenController(
                Spawn(_gameAsset),
                _topBarAsset, _playerStatusIconAsset,
                _myCardPanelAsset, _traitSlotAsset,
                _revealPanelAsset,
                _discussionPanelAsset,
                _votingPanelAsset, _voteTargetItemAsset,
                _roundResultPanelAsset,
                _specialCardModalAsset);
            var gameOver = new GameOverScreenController(Spawn(_gameOverAsset));
            var settings = new SettingsScreenController(Spawn(_settingsAsset));

            Overlay = new GlobalOverlayController(_globalOverlayAsset.CloneTree());
            _overlayLayer.Add(Overlay.Root);

            _screensByType = new Dictionary<Type, ScreenController>
            {
                { typeof(MainMenuScreenController), mainMenu },
                { typeof(JoinLobbyScreenController), joinLobby },
                { typeof(LobbyScreenController), lobby },
                { typeof(GameScreenController), game },
                { typeof(GameOverScreenController), gameOver },
                { typeof(SettingsScreenController), settings },
            };
        }

        private VisualElement Spawn(VisualTreeAsset asset)
        {
            var instance = asset.CloneTree();
            instance.style.flexGrow = 1;
            _screenLayer.Add(instance);
            return instance;
        }

        private void Start() => ShowScreen<MainMenuScreenController>();

        public T ShowScreen<T>() where T : ScreenController
        {
            _currentScreen?.Hide();
            var screen = _screensByType[typeof(T)];
            screen.Show();
            _currentScreen = screen;
            return (T)screen;
        }
    }
}