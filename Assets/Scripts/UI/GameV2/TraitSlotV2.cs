using System.Collections;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // One row of a personnel dossier.
    //
    // Prefab: Slot_TraitRow. Label column is a fixed 220 wide; the value column
    // is flexible and drives the row height, so a one-word Gender and a
    // three-line Profession share the same grid without either being truncated.
    public class TraitSlotV2 : MonoBehaviour
    {
        public enum SlotState { Hidden, Revealed, JustRevealed, Declassified }

        private const float JustRevealedHold = 2.5f;
        private const float JustRevealedFade = 0.4f;
        private const float CensorWipe = 0.12f;
        private const float ValueRise = 0.18f;

        [Header("Identity")]
        [Tooltip("Which trait this row shows. Set per instance inside the card prefab.")]
        public CardCategory Category;

        [Header("Refs")]
        [SerializeField] private Plate _plate;
        [SerializeField] private TMP_Text _categoryLabel;
        [SerializeField] private RectTransform _valueColumn;
        [SerializeField] private TMP_Text _valueLabel;
        [SerializeField] private TMP_Text _justRevealedLabel;
        [SerializeField] private RectTransform _censorBar;
        [SerializeField] private TMP_Text _classifiedLabel;
        [SerializeField] private GameObject _checkIcon;

        private SlotState _state = SlotState.Hidden;
        private Coroutine _decay;
        private Vector2 _valueColumnRest;
        private bool _restCaptured;

        private void Awake()
        {
            if (_valueColumn != null && !_restCaptured)
            {
                _valueColumnRest = _valueColumn.anchoredPosition;
                _restCaptured = true;
            }
        }

        private void OnEnable()
        {
            // Localization no-ops while hidden, so the static labels are re-run
            // every time the slot comes back on screen.
            LocText.Set(this, _categoryLabel, LocKeys.Category(Category));
            LocText.Set(this, _classifiedLabel, LocKeys.Classified);
        }

        public void Apply(CharacterTrait trait, SlotState state, bool animateReveal)
        {
            bool wasHidden = _state == SlotState.Hidden;
            _state = state;

            if (_decay != null)
            {
                StopCoroutine(_decay);
                _decay = null;
            }

            bool hidden = state == SlotState.Hidden;
            _censorBar.gameObject.SetActive(hidden);
            _valueLabel.gameObject.SetActive(!hidden);
            _justRevealedLabel.gameObject.SetActive(state == SlotState.JustRevealed);
            _checkIcon.SetActive(state == SlotState.Revealed);

            if (hidden)
            {
                PaintHidden();
                _censorBar.localScale = Vector3.one;
                return;
            }

            if (trait != null) LocText.SetContent(this, _valueLabel, trait.LocalizationKey);
            else _valueLabel.text = string.Empty;

            switch (state)
            {
                case SlotState.JustRevealed:
                    PaintJustRevealed();
                    LocText.Set(this, _justRevealedLabel, LocKeys.RevealedThisTurn);
                    if (isActiveAndEnabled) _decay = StartCoroutine(DecayToRevealed(animateReveal && wasHidden));
                    else PaintRevealed();
                    break;

                case SlotState.Declassified:
                    PaintDeclassified();
                    break;

                default:
                    PaintRevealed();
                    break;
            }
        }

        /// Loading: plates present, value blank, no censor bar — a skeleton must
        /// never be mistaken for a classified field.
        public void ApplySkeleton()
        {
            _state = SlotState.Hidden;
            _censorBar.gameObject.SetActive(false);
            _justRevealedLabel.gameObject.SetActive(false);
            _checkIcon.SetActive(false);
            _valueLabel.gameObject.SetActive(true);
            _valueLabel.text = string.Empty;
            _plate.Set(BunkerTheme.HeaderPlate, BunkerTheme.SunkenBorder);
            _categoryLabel.color = BunkerTheme.Skeleton;
        }

        // --- Painting ---------------------------------------------------------

        private void PaintHidden()
        {
            _plate.Set(BunkerTheme.Sunken, BunkerTheme.SunkenBorder);
            _plate.SetBorderWidth(1f);
            _categoryLabel.color = BunkerTheme.Stroke;
        }

        private void PaintRevealed()
        {
            _plate.Set(BunkerTheme.SurfaceRaised, BunkerTheme.Border);
            _plate.SetBorderWidth(1f);
            _categoryLabel.color = BunkerTheme.TextSecondary;
            _valueLabel.color = BunkerTheme.TextPrimary;
        }

        private void PaintJustRevealed()
        {
            _plate.Set(BunkerTheme.AccentPlate, BunkerTheme.Accent);
            _plate.SetBorderWidth(2f);
            _categoryLabel.color = BunkerTheme.Accent;
            _valueLabel.color = BunkerTheme.AccentValue;
        }

        private void PaintDeclassified()
        {
            _plate.Set(BunkerTheme.Sunken, BunkerTheme.SunkenBorder);
            _plate.SetBorderWidth(1f);
            _categoryLabel.color = BunkerTheme.Stroke;
            _valueLabel.color = BunkerTheme.TextSecondary;
        }

        // The censor bar wipes out from the centre, the value rises into place,
        // then the amber emphasis decays back to a plain revealed slot. No 3D
        // flip: these are rows, not cards.
        private IEnumerator DecayToRevealed(bool animateWipe)
        {
            if (animateWipe)
            {
                _censorBar.gameObject.SetActive(true);
                float t = 0f;
                while (t < CensorWipe)
                {
                    t += Time.deltaTime;
                    _censorBar.localScale = new Vector3(1f - Mathf.Clamp01(t / CensorWipe), 1f, 1f);
                    yield return null;
                }
                _censorBar.gameObject.SetActive(false);
                _censorBar.localScale = Vector3.one;

                t = 0f;
                while (t < ValueRise)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / ValueRise);
                    _valueLabel.alpha = k;
                    if (_valueColumn != null)
                    {
                        _valueColumn.anchoredPosition = _valueColumnRest + new Vector2(0f, Mathf.Lerp(-8f, 0f, k));
                    }
                    yield return null;
                }
                _valueLabel.alpha = 1f;
                if (_valueColumn != null) _valueColumn.anchoredPosition = _valueColumnRest;
            }

            yield return new WaitForSeconds(JustRevealedHold);

            float f = 0f;
            Color fromFill = _plate.Fill.color, fromBorder = _plate.Border.color;
            Color fromLabel = _categoryLabel.color, fromValue = _valueLabel.color;

            while (f < JustRevealedFade)
            {
                f += Time.deltaTime;
                float k = Mathf.Clamp01(f / JustRevealedFade);
                _plate.Set(
                    Color.Lerp(fromFill, BunkerTheme.SurfaceRaised, k),
                    Color.Lerp(fromBorder, BunkerTheme.Border, k));
                _categoryLabel.color = Color.Lerp(fromLabel, BunkerTheme.TextSecondary, k);
                _valueLabel.color = Color.Lerp(fromValue, BunkerTheme.TextPrimary, k);
                _justRevealedLabel.alpha = 1f - k;
                yield return null;
            }

            _justRevealedLabel.gameObject.SetActive(false);
            _justRevealedLabel.alpha = 1f;
            _state = SlotState.Revealed;
            _checkIcon.SetActive(true);
            PaintRevealed();
            _decay = null;
        }
    }
}
