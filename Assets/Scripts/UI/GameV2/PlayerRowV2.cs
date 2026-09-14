using System;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // One row of the expanded roster. Every row is tappable — a dead player's
    // file stays readable — and every row carries a chevron plus a border, so
    // the affordance never rests on colour alone.
    //
    // Prefab: PlayerListRow. Min height 120 (above the 88 touch floor), but the
    // name wraps to two lines rather than truncating, so a long Ukrainian name
    // grows the row instead of being cut.
    public class PlayerRowV2 : MonoBehaviour
    {
        [SerializeField] private Plate _plate;
        [SerializeField] private PlayerPip _pip;
        [SerializeField] private GameObject _eliminatedHatch;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private GameObject _youTag;
        [SerializeField] private TMP_Text _youLabel;
        [SerializeField] private GameObject _expelledStamp;
        [SerializeField] private TMP_Text _expelledLabel;
        [SerializeField] private Image _chevron;
        [SerializeField] private Button _button;
        [SerializeField] private PressTint _pressTint;

        private Action<string> _onSelected;
        private string _playerId;

        public string PlayerId => _playerId;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(() => _onSelected?.Invoke(_playerId));
        }

        public void SetCallback(Action<string> onSelected) => _onSelected = onSelected;

        public void Bind(PlayerData player, bool isCurrentTurn, bool isLocal, bool isNextUp,
                         int revealed, int total, int eliminatedRound, MonoBehaviour host)
        {
            _playerId = player.PlayerId;
            bool eliminated = player.IsEliminated;

            _pip.Apply(isCurrentTurn, eliminated, isLocal);
            _eliminatedHatch.SetActive(eliminated);
            _youTag.SetActive(isLocal && !eliminated);
            _expelledStamp.SetActive(eliminated);

            _nameLabel.text = player.DisplayName ?? string.Empty;

            if (eliminated)
            {
                _plate.Set(BunkerTheme.Sunken, BunkerTheme.SunkenBorder);
                _plate.SetBorderWidth(1f);
                _nameLabel.color = BunkerTheme.Stroke;
                _nameLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase | FontStyles.Strikethrough;
                _statusLabel.color = BunkerTheme.Stroke;
                _chevron.color = BunkerTheme.Stroke;
                LocText.Set(host, _statusLabel, LocKeys.FileClosed, eliminatedRound);
                LocText.Set(host, _expelledLabel, LocKeys.Expelled);
            }
            else if (isCurrentTurn)
            {
                _plate.Set(BunkerTheme.AccentPlate, BunkerTheme.Accent);
                _plate.SetBorderWidth(2f);
                _nameLabel.color = BunkerTheme.Accent;
                _nameLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                _statusLabel.color = BunkerTheme.Accent;
                _chevron.color = BunkerTheme.Accent;
                LocText.Set(host, _statusLabel, LocKeys.RevealingNow, revealed, total);
            }
            else
            {
                _plate.Set(BunkerTheme.SurfaceRaised, BunkerTheme.Border);
                _plate.SetBorderWidth(1f);
                _nameLabel.color = BunkerTheme.TextPrimary;
                _nameLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                _statusLabel.color = BunkerTheme.TextSecondary;
                _chevron.color = BunkerTheme.TextSecondary;
                LocText.Set(host, _statusLabel, isNextUp ? LocKeys.NextUp : LocKeys.RevealedCount, revealed, total);
            }

            if (isLocal && !eliminated) LocText.Set(host, _youLabel, LocKeys.You);

            // The press tint samples the plate's resting colours, so it has to be
            // re-captured every time the row is repainted for a new state.
            if (_pressTint != null) _pressTint.CaptureRest();
        }
    }
}
