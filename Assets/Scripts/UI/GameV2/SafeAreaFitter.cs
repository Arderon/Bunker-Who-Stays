using UnityEngine;

namespace Bunker.UI.GameV2
{
    // Insets a full-screen RectTransform to the device's safe area, so nothing
    // lands under a notch, a punch-hole camera or the home indicator.
    //
    // The spec budgets 110px for the status bar and 56px for the home indicator
    // at the 1080x1920 reference. Those are the fallback when the platform
    // reports no cutouts (editor, desktop, older Android), which keeps the
    // layout identical to the spec while still yielding to a real notch.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Applied when the platform reports no safe-area inset, in reference pixels.")]
        [SerializeField] private float _fallbackTop = BunkerTheme.StatusBarHeight;
        [SerializeField] private float _fallbackBottom = BunkerTheme.HomeIndicator;

        [Tooltip("Leave the sides full-bleed. The reveal panel and scrims run edge to edge by design.")]
        [SerializeField] private bool _applyHorizontal;

        private RectTransform _rect;
        private Canvas _canvas;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            Apply();
        }

        // Orientation changes and Android multi-window resizes both arrive as a
        // new safe area without any other notification, so this polls rather
        // than subscribing to something that does not exist.
        private void Update()
        {
            if (Screen.safeArea == _lastSafeArea &&
                Screen.width == _lastResolution.x &&
                Screen.height == _lastResolution.y)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;

            // Safe area is in screen pixels; the layout is in canvas reference
            // units, so convert through the scaler's factor.
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

            float top = (Screen.height - (safeArea.y + safeArea.height)) / scale;
            float bottom = safeArea.y / scale;
            float left = safeArea.x / scale;
            float right = (Screen.width - (safeArea.x + safeArea.width)) / scale;

            if (top <= 0.5f) top = _fallbackTop;
            if (bottom <= 0.5f) bottom = _fallbackBottom;

            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.offsetMin = new Vector2(_applyHorizontal ? left : 0f, bottom);
            _rect.offsetMax = new Vector2(_applyHorizontal ? -right : 0f, -top);
        }
    }
}
