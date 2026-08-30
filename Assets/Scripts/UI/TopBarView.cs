using Bunker.Core;
using Bunker.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _roundLabel;
        [SerializeField] private Transform _playerStatusStripContainer;
        [SerializeField] private PlayerStatusIcon _playerStatusIconPrefab;

        private GameSession _session;

        public void Bind(GameSession session)
        {
            _session = session;
            _session.OnPlayerEliminated += _ => RefreshPlayerStrip();

            RefreshRound(session.CurrentRound);
            RefreshPlayerStrip();
        }

        public void RefreshRound(int round)
        {
            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_game_round_label",
                text => _roundLabel.text = text, round));
        }

        private void RefreshPlayerStrip()
        {
            foreach (Transform child in _playerStatusStripContainer) Destroy(child.gameObject);

            foreach (var player in _session.Players)
            {
                var icon = Instantiate(_playerStatusIconPrefab, _playerStatusStripContainer);
                icon.Bind(player);
            }
        }
    }
}