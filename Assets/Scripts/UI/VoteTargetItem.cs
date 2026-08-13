using System;
using UnityEngine;
using UnityEngine.UI;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class VoteTargetItem : MonoBehaviour
    {
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Button _voteButton;
        [SerializeField] private Text _voteButtonLabel;

        public void Bind(PlayerData player, Action<PlayerData> onVoteClicked)
        {
            _nameLabel.text = player.DisplayName;

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_voting_vote_button", text => _voteButtonLabel.text = text));

            _voteButton.onClick.AddListener(() => onVoteClicked(player));
        }
    }
}