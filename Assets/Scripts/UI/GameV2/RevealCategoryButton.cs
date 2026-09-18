using System;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // One cell of the reveal grid. Four states, none carried by colour alone:
    // enabled is the only amber-outlined thing on screen, locked is struck
    // through and carries a "done" line, inert is sunken and unlit.
    //
    // Prefab: RevealCategoryButton. The label auto-sizes with a 24 floor and
    // wraps to two lines, so a 14-character Ukrainian category fits the 238-wide
    // cell without truncation.
    public class RevealCategoryButton : MonoBehaviour
    {
        // Public surface RevealPhasePanelV2 drives — unchanged, so callers never
        // see the visual split below.
        public enum State { Enabled, Locked, Inert }

        // Locked has two real appearances depending on whether the panel is
        // currently addressed to you: bright when it's your own turn and this
        // trait is already revealed, dim everywhere else (someone else's turn,
        // or the reveal pass is over). That is a second axis the public State
        // enum deliberately doesn't expose — this key is where it gets resolved
        // into one of four resting looks before the Inspector table is consulted.
        // Public only so Visual (a nested public struct) can implement
        // IStateVisual<VisualKey> without an accessibility mismatch.
        public enum VisualKey { Enabled, Locked, LockedDimmed, Inert }

        // Resting appearance for one key, configured in the Inspector.
        [Serializable]
        public struct Visual : IStateVisual<VisualKey>
        {
            public VisualKey key;
            public VisualKey State => key;

            public PlateVisual plate;
            public LabelVisual label;
            public bool showDoneLine;
            public LabelVisual doneLine;
        }

        [Tooltip("Which trait this cell reveals. Set per instance in the grid.")]
        public CardCategory Category;

        [SerializeField] private Plate _plate;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject _doneLine;
        [SerializeField] private TMP_Text _doneLabel;
        [SerializeField] private Button _button;
        [SerializeField] private PressTint _pressTint;

        [Header("Стани (заповнити в інспекторі): Enabled, Locked, LockedDimmed, Inert")]
        [SerializeField] private Visual[] _visuals;

        private Action<CardCategory> _onClick;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke(Category));
        }

        private void OnEnable() => LocText.Set(this, _label, LocKeys.Category(Category));

        public void SetCallback(Action<CardCategory> onClick) => _onClick = onClick;

        public void Apply(State state, bool dimmed)
        {
            if (_button != null) _button.interactable = state == State.Enabled;

            var key = state switch
            {
                State.Enabled => VisualKey.Enabled,
                State.Locked => dimmed ? VisualKey.LockedDimmed : VisualKey.Locked,
                _ => VisualKey.Inert
            };

            var visual = StateVisual.Find(_visuals, key, this);
            visual.plate.ApplyTo(_plate);
            visual.label.ApplyTo(_label);

            _doneLine.SetActive(visual.showDoneLine);
            if (visual.showDoneLine)
            {
                visual.doneLine.ApplyTo(_doneLabel);
                LocText.Set(this, _doneLabel, LocKeys.Done);
            }

            if (_pressTint != null) _pressTint.CaptureRest();
        }
    }
}
