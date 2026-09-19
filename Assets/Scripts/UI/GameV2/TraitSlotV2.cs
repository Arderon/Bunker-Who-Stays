using System;
using System.Collections;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
        public enum SlotState
        {
            /// Someone else's unrevealed trait: censored, unreadable.
            Hidden,

            /// Your own unrevealed trait. You know your own character, so the
            /// value is legible — but it is not public yet, so it is styled as
            /// withheld and carries a "only you can see this" hint.
            Private,

            Revealed,
            JustRevealed,

            /// Eliminated player: everything declassifies, nothing is hidden.
            Declassified,

            /// Data has not arrived yet. Must not look like Hidden — a skeleton
            /// is not a classified field.
            Loading
        }

        // Resting appearance for one state, configured in the Inspector —
        // nothing here is a BunkerTheme constant baked into code, so restyling
        // a slot never requires touching this script.
        [Serializable]
        public struct Visual : IStateVisual<SlotState>
        {
            public SlotState state;
            public SlotState State => state;

            public PlateVisual plate;
            public LabelVisual categoryLabel;
            public LabelVisual valueLabel;

            [Tooltip("Hidden only: a filled hatched bar, not an outline — withheld must not read as empty.")]
            public bool showCensorBar;
            public bool showCheckIcon;

            [Header("Другий рядок (підказка)")]
            public LabelVisual hintLabel;
            [Tooltip("Ключ у UI-Table, напр. ui_v2_revealed_this_turn або ui_v2_private_trait. " +
                     "Порожньо — рядок сховано.")]
            public string hintKey;
        }

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
        [FormerlySerializedAs("_justRevealedLabel")]
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private RectTransform _censorBar;
        [SerializeField] private TMP_Text _classifiedLabel;
        [SerializeField] private GameObject _checkIcon;

        [Header("Стани (заповнити в інспекторі)")]
        [SerializeField] private Visual[] _visuals;

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
            bool wasConcealed = _state == SlotState.Hidden || _state == SlotState.Private;
            _state = state;

            if (_decay != null)
            {
                StopCoroutine(_decay);
                _decay = null;
            }

            ApplyVisual(StateVisual.Find(_visuals, state, this));

            if (state == SlotState.Hidden)
            {
                _censorBar.localScale = Vector3.one;
                return;
            }

            // Private, Revealed, JustRevealed and Declassified all show the real
            // value; only Hidden withholds it.
            if (trait != null) LocText.SetContent(this, _valueLabel, trait.LocalizationKey);
            else _valueLabel.text = string.Empty;

            if (state == SlotState.JustRevealed)
            {
                if (isActiveAndEnabled) _decay = StartCoroutine(DecayToRevealed(animateReveal && wasConcealed));
                else ApplyVisual(StateVisual.Find(_visuals, SlotState.Revealed, this));
            }
        }

        /// Loading: plates present, value blank, no censor bar.
        public void ApplySkeleton()
        {
            _state = SlotState.Loading;
            if (_decay != null)
            {
                StopCoroutine(_decay);
                _decay = null;
            }

            ApplyVisual(StateVisual.Find(_visuals, SlotState.Loading, this));
            _valueLabel.text = string.Empty;
        }

        private void ApplyVisual(Visual visual)
        {
            visual.plate.ApplyTo(_plate);
            visual.categoryLabel.ApplyTo(_categoryLabel);
            visual.valueLabel.ApplyTo(_valueLabel);

            _censorBar.gameObject.SetActive(visual.showCensorBar);
            _valueLabel.gameObject.SetActive(!visual.showCensorBar);
            _checkIcon.SetActive(visual.showCheckIcon);

            bool hasHint = !string.IsNullOrEmpty(visual.hintKey);
            _hintLabel.gameObject.SetActive(hasHint);
            if (hasHint)
            {
                visual.hintLabel.ApplyTo(_hintLabel);
                _hintLabel.alpha = 1f;
                LocText.Set(this, _hintLabel, visual.hintKey);
            }
        }

        // The censor bar wipes out from the centre, the value rises into place,
        // then the amber emphasis decays back to a plain revealed slot. No 3D
        // flip: these are rows, not cards. The end colours come from the
        // configured Revealed visual, not a hardcoded constant, so a restyle in
        // the Inspector is reflected here automatically.
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

            var from = StateVisual.Find(_visuals, SlotState.JustRevealed, this);
            var to = StateVisual.Find(_visuals, SlotState.Revealed, this);

            float f = 0f;
            while (f < JustRevealedFade)
            {
                f += Time.deltaTime;
                float k = Mathf.Clamp01(f / JustRevealedFade);
                _plate.Set(
                    Color.Lerp(from.plate.fill, to.plate.fill, k),
                    Color.Lerp(from.plate.border, to.plate.border, k));
                _plate.SetBorderWidth(Mathf.Lerp(from.plate.borderWidth, to.plate.borderWidth, k));
                _categoryLabel.color = Color.Lerp(from.categoryLabel.color, to.categoryLabel.color, k);
                _valueLabel.color = Color.Lerp(from.valueLabel.color, to.valueLabel.color, k);
                _hintLabel.alpha = 1f - k;
                yield return null;
            }

            _state = SlotState.Revealed;
            ApplyVisual(to);
            _decay = null;
        }
    }
}
