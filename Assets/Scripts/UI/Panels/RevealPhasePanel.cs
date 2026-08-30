using Bunker.Core;
using Bunker.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class RevealPhasePanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _turnIndicatorLabel;
        [SerializeField] private Button[] _categoryButtons;
        [SerializeField] private Button _startDiscussionButton;

        private GameSession _session;
        private string _localPlayerId;

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;

            session.OnRoundStarted += _ => Refresh();
            session.OnTraitRevealed += (_, _) => Refresh();
            session.OnRevealPassCompleted += OnRevealPassCompleted;

            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                var category = (CardCategory)i;
                _categoryButtons[i].onClick.AddListener(() =>
                    _session.RevealNextTrait(_localPlayerId, category));
            }

            _startDiscussionButton.gameObject.SetActive(false);
            _startDiscussionButton.onClick.AddListener(() => _session.StartDiscussionPhase(durationSeconds: 120));
        }

        private void OnEnable()
        {
            if (_session != null) Refresh();
        }

        private void Refresh()
        {
            bool isMyTurn = _session.CurrentTurnPlayerId == _localPlayerId;

            var key = isMyTurn ? "ui_game_your_turn" : "ui_game_waiting_for_player";
            var currentPlayer = _session.GetPlayer(_session.CurrentTurnPlayerId);

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, key, text => _turnIndicatorLabel.text = text,
                isMyTurn ? System.Array.Empty<object>() : new object[] { currentPlayer?.DisplayName }));

            var localPlayer = _session.GetPlayer(_localPlayerId);
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                var category = (CardCategory)i;
                bool alreadyRevealed = localPlayer.IsCategoryRevealed(category);
                _categoryButtons[i].interactable = isMyTurn && !alreadyRevealed;
            }
        }

        private void OnRevealPassCompleted()
        {
            // For MVP: only the host's button actually triggers the transition;
            // non-host players just see the panel until the host acts.
            // Host detection is out of scope for GameSession itself — resolved
            // by the network layer in stage 4/5. For now, show it to everyone
            // in local hot-seat testing.
            _startDiscussionButton.gameObject.SetActive(true);
        }
    }
}