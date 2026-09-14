using Bunker.Core;
using UnityEngine;

namespace Bunker.UI.GameV2
{
    // Wires the V2 screen into the existing flow. Put this on the UI Canvas and
    // drag the GameScreenV2 object into it.
    //
    // Untick Enabled (or remove the component) and the original prefab-driven
    // GameScreen runs exactly as it did before — LobbyScreen falls through to it
    // whenever TryShowGame returns false.
    //
    // Known seam: the spec only covers the reveal phase, so V2 owns Reveal and
    // hands off once to the legacy GameScreen when the game reaches discussion.
    // The handoff is one-way per game; the legacy screen then runs discussion,
    // voting and results, including later reveal passes.
    public class GameScreenV2Host : MonoBehaviour
    {
        [Tooltip("Untick to fall back to the original GameScreen.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("The GameScreenV2 object in this canvas.")]
        [SerializeField] private GameScreenV2 _screen;

        public static GameScreenV2Host Instance { get; private set; }

        private IGameSessionView _session;
        private bool _handedOff;

        public bool IsEnabled => _enabled && _screen != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (!IsEnabled) return;

            _screen.Hide();
            UIManager.Instance?.RegisterScreen(_screen);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_session != null) _session.OnPhaseChanged -= OnPhaseChanged;
        }

        /// Returns false when V2 is not installed, so the caller falls through to
        /// the original screen untouched.
        public static bool TryShowGame(IGameSessionView session, string localPlayerId = null)
        {
            var host = Instance;
            if (host == null || !host.IsEnabled || session == null) return false;

            host.Begin(session, localPlayerId);
            return true;
        }

        private void Begin(IGameSessionView session, string localPlayerId)
        {
            if (_session != null) _session.OnPhaseChanged -= OnPhaseChanged;

            _session = session;
            _handedOff = false;
            _session.OnPhaseChanged += OnPhaseChanged;

            if (session.Phase != GamePhase.Reveal)
            {
                HandOffToLegacy();
                return;
            }

            UIManager.Instance.RegisterScreen(_screen);
            UIManager.Instance.ShowScreen<GameScreenV2>().Bind(session, localPlayerId);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (_handedOff || phase == GamePhase.Reveal) return;
            HandOffToLegacy();
        }

        private void HandOffToLegacy()
        {
            _handedOff = true;
            if (_session != null) _session.OnPhaseChanged -= OnPhaseChanged;

            UIManager.Instance.ShowScreen<GameScreen>().Bind(_session);
        }
    }
}
