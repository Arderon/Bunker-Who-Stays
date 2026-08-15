using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bunker.UI
{
    // Single entry point for switching between full-screen UIScreens and
    // showing global overlays (toasts, loading spinner, connection status).
    // Other scripts never call gameObject.SetActive on screens directly —
    // they go through this manager, so exactly one screen is visible at a time.
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Screens")]
        [SerializeField] private MainMenuScreen _mainMenuScreen;
        [SerializeField] private JoinLobbyScreen _joinLobbyScreen;
        [SerializeField] private LobbyScreen _lobbyScreen;
        [SerializeField] private GameScreen _gameScreen;
        [SerializeField] private GameOverScreen _gameOverScreen;
        [SerializeField] private SettingsScreen _settingsScreen;

        [Header("Global Overlay")]
        [SerializeField] private GlobalOverlay _globalOverlay;

        private UIScreen _currentScreen;
        private Dictionary<Type, UIScreen> _screensByType;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _screensByType = new Dictionary<Type, UIScreen>
            {
                { typeof(MainMenuScreen), _mainMenuScreen },
                { typeof(JoinLobbyScreen), _joinLobbyScreen },
                { typeof(LobbyScreen), _lobbyScreen },
                { typeof(GameScreen), _gameScreen },
                { typeof(GameOverScreen), _gameOverScreen },
                { typeof(SettingsScreen), _settingsScreen },
            };

            foreach (var screen in _screensByType.Values)
            {
                screen.Hide();
            }
        }

        private void Start()
        {
            ShowScreen<MainMenuScreen>();
        }

        public T ShowScreen<T>() where T : UIScreen
        {
            _currentScreen?.Hide();

            var screen = _screensByType[typeof(T)];
            screen.Show();
            _currentScreen = screen;

            return (T)screen;
        }

        public GlobalOverlay Overlay => _globalOverlay;
    }
}