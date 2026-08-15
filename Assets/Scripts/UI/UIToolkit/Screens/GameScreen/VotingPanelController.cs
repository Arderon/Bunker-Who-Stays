using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class VotingPanelController
    {
        public VisualElement Root { get; }

        private readonly Label _titleLabel, _promptLabel, _youVotedLabel;
        private readonly ListView _targetsList;
        private readonly Button _resolveButton;
        private readonly VisualTreeAsset _targetItemAsset;

        private GameSession _session;
        private string _localPlayerId;
        private List<PlayerData> _candidates = new();

        public VotingPanelController(VisualElement root, VisualTreeAsset targetItemAsset)
        {
            Root = root;
            _targetItemAsset = targetItemAsset;

            _titleLabel = root.Q<Label>("title-label");
            _promptLabel = root.Q<Label>("prompt-label");
            _targetsList = root.Q<ListView>("targets-list");
            _youVotedLabel = root.Q<Label>("you-voted-label");
            _resolveButton = root.Q<Button>("resolve-button");

            _targetsList.makeItem = () => _targetItemAsset.CloneTree();
            _targetsList.bindItem = (element, index) => BindTarget(element, _candidates[index]);
            _targetsList.itemsSource = _candidates;

            _resolveButton.clicked += () => _session.ResolveVotes();
            RefreshStaticText();
        }

        private async void RefreshStaticText()
        {
            _promptLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_voting_prompt");
            _resolveButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_common_confirm");
        }

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;
        }

        // Called explicitly on entering Voting/VotingTiebreaker (see
        // DiscussionPanelController note above — same reason).
        public async void NotifyPhaseEntered(bool isTiebreaker)
        {
            var key = isTiebreaker ? "ui_voting_tie_title" : "ui_voting_title";
            _titleLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
            _youVotedLabel.style.display = DisplayStyle.None;

            _candidates = _session.ActivePlayers().Where(p => p.PlayerId != _localPlayerId).ToList();
            _targetsList.itemsSource = _candidates;
            _targetsList.RefreshItems();
        }

        private async void BindTarget(VisualElement element, PlayerData target)
        {
            element.Q<Label>("name-label").text = target.DisplayName;
            var voteButton = element.Q<Button>("vote-button");
            voteButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_voting_vote_button");

            // ListView recycles elements — remove the previous row's handler
            // before attaching a new one, or clicks stack up on reused rows.
            if (voteButton.userData is System.Action previousHandler)
                voteButton.clicked -= previousHandler;

            System.Action handler = () => OnVoteClicked(target);
            voteButton.userData = handler;
            voteButton.clicked += handler;
        }

        private async void OnVoteClicked(PlayerData target)
        {
            bool success = _session.CastVote(_localPlayerId, target.PlayerId);
            if (!success)
            {
                UIManager.Instance.Overlay.ShowToast("ui_common_error_generic");
                return;
            }

            _youVotedLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_voting_you_voted", target.DisplayName);
            _youVotedLabel.style.display = DisplayStyle.Flex;
        }
    }
}