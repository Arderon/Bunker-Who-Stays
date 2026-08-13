using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bunker.Core;
using Bunker.Localization;
using System.Linq;

namespace Bunker.UI
{
    public class VotingPhasePanel : MonoBehaviour
    {
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _promptLabel;
        [SerializeField] private Transform _targetListContainer;
        [SerializeField] private VoteTargetItem _targetItemPrefab;
        [SerializeField] private Text _youVotedLabel;
        [SerializeField] private Text _waitingForOthersLabel;
        [SerializeField] private Button _resolveButton; // host-only, calls ResolveVotes

        private GameSession _session;
        private string _localPlayerId;
        private readonly List<VoteTargetItem> _spawnedItems = new();

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;

            session.OnVotingResolved += OnVotingResolved;
            _resolveButton.onClick.AddListener(() => _session.ResolveVotes());
        }

        private void OnEnable()
        {
            if (_session == null) return;

            var isTiebreaker = _session.Phase == GamePhase.VotingTiebreaker;
            var titleKey = isTiebreaker ? "ui_voting_tie_title" : "ui_voting_title";

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, titleKey, text => _titleLabel.text = text));

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_voting_prompt", text => _promptLabel.text = text));

            _youVotedLabel.gameObject.SetActive(false);
            RefreshTargetList();
        }

        private void RefreshTargetList()
        {
            foreach (var item in _spawnedItems) Destroy(item.gameObject);
            _spawnedItems.Clear();

            var candidates = _session.ActivePlayers().Where(p => p.PlayerId != _localPlayerId);

            foreach (var candidate in candidates)
            {
                var item = Instantiate(_targetItemPrefab, _targetListContainer);
                item.Bind(candidate, OnVoteButtonClicked);
                _spawnedItems.Add(item);
            }
        }

        private void OnVoteButtonClicked(PlayerData target)
        {
            bool success = _session.CastVote(_localPlayerId, target.PlayerId);
            if (!success)
            {
                UIManager.Instance.Overlay.ShowToast("ui_common_error_generic");
                return;
            }

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_voting_you_voted",
                text => _youVotedLabel.text = text, target.DisplayName));
            _youVotedLabel.gameObject.SetActive(true);
        }

        private void OnVotingResolved(VotingResult result)
        {
            // RoundResultPanel handles the outcome message; this panel just
            // needs to reset itself for a possible tiebreaker re-entry.
        }
    }
}