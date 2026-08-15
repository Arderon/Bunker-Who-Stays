using System.Linq;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class RoundResultPanelController
    {
        public VisualElement Root { get; }
        private readonly Label _titleLabel, _outcomeLabel, _votesBreakdownLabel;
        private GameSession _session;

        public RoundResultPanelController(VisualElement root)
        {
            Root = root;
            _titleLabel = root.Q<Label>("title-label");
            _outcomeLabel = root.Q<Label>("outcome-label");
            _votesBreakdownLabel = root.Q<Label>("votes-breakdown-label");
            var continueButton = root.Q<Button>("continue-button");

            RefreshStaticText(continueButton);
        }

        private async void RefreshStaticText(Button continueButton)
        {
            _titleLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_round_result_title");
            continueButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_round_result_continue");
        }

        public void Bind(GameSession session)
        {
            _session = session;
            session.OnVotingResolved += OnVotingResolved;
        }

        private async void OnVotingResolved(VotingResult result)
        {
            var votesText = string.Join(", ", result.VoteCounts.Select(kv =>
                $"{_session.GetPlayer(kv.Key)?.DisplayName ?? kv.Key}: {kv.Value}"));

            _votesBreakdownLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_round_result_votes_breakdown", votesText);

            switch (result.ResultType)
            {
                case VotingResult.Outcome.PlayerEliminated:
                    _outcomeLabel.text = await LocalizedTextService.GetTextAsync(
                        LocalizationTableNames.UI, "ui_round_result_eliminated", result.EliminatedPlayer.DisplayName);
                    break;

                case VotingResult.Outcome.TieUnresolvedNoElimination:
                case VotingResult.Outcome.NoVotesCast:
                    _outcomeLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_voting_tie_no_elimination");
                    break;

                case VotingResult.Outcome.TieRequiresRevote:
                    break; // panel isn't shown for this case — VotingPanel reappears instead
            }
        }
    }
}