using UnityEngine;
using UnityEngine.UI;
using Bunker.Core;
using Bunker.Localization;
using System.Linq;

namespace Bunker.UI
{
    public class RoundResultPanel : MonoBehaviour
    {
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _outcomeLabel;
        [SerializeField] private Text _votesBreakdownLabel;
        [SerializeField] private Button _continueButton;

        private GameSession _session;

        public void Bind(GameSession session)
        {
            _session = session;
            session.OnVotingResolved += OnVotingResolved;

            _continueButton.onClick.AddListener(() =>
            {
                // GameSession already auto-advances after resolution (section 1.6);
                // this button exists purely so the player has to actively
                // acknowledge the result before the next round's Reveal begins.
                // If GameSession has already moved on, this simply becomes a no-op
                // visual dismissal handled by GameScreen's phase switch.
            });

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_round_result_title", text => _titleLabel.text = text));
        }

        private void OnVotingResolved(VotingResult result)
        {
            var votesText = string.Join(", ", result.VoteCounts.Select(kv =>
            {
                var player = _session.GetPlayer(kv.Key);
                return $"{player?.DisplayName ?? kv.Key}: {kv.Value}";
            }));

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_round_result_votes_breakdown",
                text => _votesBreakdownLabel.text = text, votesText));

            switch (result.ResultType)
            {
                case VotingResult.Outcome.PlayerEliminated:
                    StartCoroutine(LocalizedTextService.GetTextCoroutine(
                        LocalizationTableNames.UI, "ui_round_result_eliminated",
                        text => _outcomeLabel.text = text, result.EliminatedPlayer.DisplayName));
                    break;

                case VotingResult.Outcome.TieUnresolvedNoElimination:
                case VotingResult.Outcome.NoVotesCast:
                    StartCoroutine(LocalizedTextService.GetTextCoroutine(
                        LocalizationTableNames.UI, "ui_voting_tie_no_elimination",
                        text => _outcomeLabel.text = text));
                    break;

                case VotingResult.Outcome.TieRequiresRevote:
                    // Handled by VotingPhasePanel re-appearing (phase switches
                    // back to VotingTiebreaker in GameSession) — RoundResultPanel
                    // never actually gets shown for this case.
                    break;
            }
        }
    }
}