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
        public enum State { Enabled, Locked, Inert }

        [Tooltip("Which category this cell reveals. Set per instance in the grid.")]
        public CardCategory Category;

        [SerializeField] private Plate _plate;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject _doneLine;
        [SerializeField] private TMP_Text _doneLabel;
        [SerializeField] private Button _button;
        [SerializeField] private PressTint _pressTint;

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
            _doneLine.SetActive(state == State.Locked);

            switch (state)
            {
                case State.Enabled:
                    _plate.Set(BunkerTheme.AccentPlate, BunkerTheme.Accent);
                    _plate.SetBorderWidth(2f);
                    _label.color = BunkerTheme.Accent;
                    _label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    break;

                case State.Locked:
                    _plate.Set(dimmed ? BunkerTheme.InertPlate : BunkerTheme.SurfaceRaised,
                        dimmed ? BunkerTheme.SunkenBorder : BunkerTheme.StrokeDim);
                    _plate.SetBorderWidth(1f);
                    _label.color = dimmed ? BunkerTheme.StrokeDim : BunkerTheme.TextDisabled;
                    _label.fontStyle = FontStyles.Bold | FontStyles.UpperCase | FontStyles.Strikethrough;
                    _doneLabel.color = dimmed ? BunkerTheme.SuccessDim : BunkerTheme.Success;
                    LocText.Set(this, _doneLabel, LocKeys.Done);
                    break;

                default:
                    _plate.Set(BunkerTheme.InertPlate, BunkerTheme.SunkenBorder);
                    _plate.SetBorderWidth(1f);
                    _label.color = BunkerTheme.TextDisabled;
                    _label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    break;
            }

            if (_pressTint != null) _pressTint.CaptureRest();
        }
    }
}
