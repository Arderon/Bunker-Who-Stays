using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // Press feedback for a plate: the fill goes solid, the border brightens, the
    // labels invert and the plate scales down a hair.
    //
    // Colours are swapped outright rather than multiplied. uGUI's built-in
    // ColorTint multiplies the graphic's own colour, and the spec's pressed
    // state — solid accent plate, black label — is brighter than the resting
    // plate, so it is simply unreachable that way. Set the Button's Transition
    // to None and let this drive it.
    //
    // Locked and inert plates get no press animation at all, per the spec:
    // set Button.interactable = false and nothing moves.
    public class PressTint : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float DownDuration = 0.08f;
        private const float UpDuration = 0.12f;
        private const float PressScale = 0.98f;

        [SerializeField] private Plate _plate;
        [SerializeField] private RectTransform _scaleRoot;
        [Tooltip("Labels that invert while pressed. Usually just the main label.")]
        [SerializeField] private TMP_Text[] _labels;
        [SerializeField] private Button _button;

        [Header("Pressed colours")]
        [SerializeField] private Color _pressedFill = new Color(1f, 0.69f, 0.18f);
        [SerializeField] private Color _pressedBorder = new Color(1f, 0.69f, 0.18f);
        [SerializeField] private Color _pressedLabel = new Color(0.043f, 0.047f, 0.043f);

        private Color _restFill, _restBorder;
        private Color[] _restLabelColors;
        private Coroutine _routine;
        private bool _held;

        private void Reset()
        {
            _scaleRoot = (RectTransform)transform;
            _button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (_scaleRoot == null) _scaleRoot = (RectTransform)transform;
            if (_button == null) _button = GetComponent<Button>();
            CaptureRest();
        }

        /// Re-reads the plate's current colours as the resting state. Call it
        /// after any code path that recolours the plate, or the next press will
        /// spring back to the wrong colour.
        public void CaptureRest()
        {
            if (_plate == null || _plate.Fill == null || _plate.Border == null) return;

            _restFill = _plate.Fill.color;
            _restBorder = _plate.Border.color;

            if (_labels == null) _labels = new TMP_Text[0];
            _restLabelColors = new Color[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
            {
                _restLabelColors[i] = _labels[i] != null ? _labels[i].color : Color.white;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.IsInteractable()) return;
            _held = true;
            Play(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_held) return;
            _held = false;
            Play(false);
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _held = false;
            ApplyRest();
        }

        private void Play(bool down)
        {
            if (!isActiveAndEnabled) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Animate(down));
        }

        private System.Collections.IEnumerator Animate(bool down)
        {
            float duration = down ? DownDuration : UpDuration;
            float elapsed = 0f;

            Color fromFill = _plate.Fill.color;
            Color fromBorder = _plate.Border.color;
            float fromScale = _scaleRoot.localScale.x;

            Color toFill = down ? _pressedFill : _restFill;
            Color toBorder = down ? _pressedBorder : _restBorder;
            float toScale = down ? PressScale : 1f;

            var fromLabels = new Color[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
            {
                fromLabels[i] = _labels[i] != null ? _labels[i].color : Color.white;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t); // ease-out

                _plate.Set(Color.Lerp(fromFill, toFill, t), Color.Lerp(fromBorder, toBorder, t));
                _scaleRoot.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);

                for (int i = 0; i < _labels.Length; i++)
                {
                    if (_labels[i] == null) continue;
                    _labels[i].color = Color.Lerp(fromLabels[i], down ? _pressedLabel : _restLabelColors[i], t);
                }

                yield return null;
            }

            if (!down) ApplyRest();
            _routine = null;
        }

        private void ApplyRest()
        {
            if (_plate == null || _plate.Fill == null) return;

            _plate.Set(_restFill, _restBorder);
            if (_scaleRoot != null) _scaleRoot.localScale = Vector3.one;

            if (_restLabelColors == null) return;
            for (int i = 0; i < _labels.Length && i < _restLabelColors.Length; i++)
            {
                if (_labels[i] != null) _labels[i].color = _restLabelColors[i];
            }
        }
    }
}
