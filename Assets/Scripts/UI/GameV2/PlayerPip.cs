using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // Roster state at a glance. Shape first, colour second: alive is a hollow
    // square, eliminated is the same square sunken with a red cross, and the
    // player taking their turn is a larger solid diamond with a halo — the only
    // filled pip, dominant by size, shape, fill and motion at once.
    //
    // "You" composes with all three rather than replacing them: a white stroke
    // plus a caret beneath, so you-and-your-turn is an amber diamond with a caret.
    //
    // Prefab: PlayerPip. Its parent Horizontal Layout Group must have
    // Child Control Width/Height OFF, because this script resizes the pip
    // between 30 and 44 directly.
    public class PlayerPip : MonoBehaviour
    {
        private const float PulsePeriod = 1.6f;
        private const float AliveSize = 30f;
        private const float TurnSize = 44f;

        [SerializeField] private RectTransform _root;
        [SerializeField] private Plate _body;
        [SerializeField] private GameObject _halo;
        [SerializeField] private GameObject _crossIcon;
        [SerializeField] private RectTransform _caret;

        private bool _pulsing;

        private void Reset() => _root = (RectTransform)transform;

        private void Awake()
        {
            if (_root == null) _root = (RectTransform)transform;
        }

        public void Apply(bool isCurrentTurn, bool isEliminated, bool isLocal)
        {
            if (_caret != null) _caret.gameObject.SetActive(isLocal);

            if (isCurrentTurn && !isEliminated)
            {
                _root.sizeDelta = new Vector2(TurnSize, TurnSize);
                _root.localRotation = Quaternion.Euler(0f, 0f, 45f);
                // The caret is a child, so it inherits the diamond's rotation —
                // counter-rotate it so it still points down at the screen.
                if (_caret != null) _caret.localRotation = Quaternion.Euler(0f, 0f, -45f);
                _body.Set(BunkerTheme.Accent, BunkerTheme.Accent);
                _halo.SetActive(true);
                _crossIcon.SetActive(false);
                _pulsing = true;
                return;
            }

            _root.sizeDelta = new Vector2(AliveSize, AliveSize);
            _root.localRotation = Quaternion.identity;
            if (_caret != null) _caret.localRotation = Quaternion.identity;
            _halo.SetActive(false);
            _pulsing = false;

            if (isEliminated)
            {
                _body.Set(BunkerTheme.PipWell, BunkerTheme.StrokeDim);
                _crossIcon.SetActive(true);
                return;
            }

            _body.Set(BunkerTheme.SurfaceRaised, isLocal ? BunkerTheme.TextPrimary : BunkerTheme.Stroke);
            _crossIcon.SetActive(false);
        }

        /// The loading bar renders pips as empty wells — a roster that has not
        /// arrived must not be mistaken for a roster of dead players.
        public void ApplyWell()
        {
            _root.sizeDelta = new Vector2(AliveSize, AliveSize);
            _root.localRotation = Quaternion.identity;
            _halo.SetActive(false);
            _crossIcon.SetActive(false);
            if (_caret != null) _caret.gameObject.SetActive(false);
            _pulsing = false;
            _body.Set(BunkerTheme.SurfaceRaised, BunkerTheme.SurfaceRaised);
        }

        private void Update()
        {
            if (!_pulsing) return;

            float k = 0.5f + 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / PulsePeriod);
            var color = BunkerTheme.Accent.WithAlpha(Mathf.Lerp(0.55f, 1f, k));
            _body.Set(color, color);
        }
    }
}
