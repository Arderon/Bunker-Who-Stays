using Bunker.Core;
using Bunker.Localization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class VoteTargetItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private Button _voteButton;
        [SerializeField] private TMP_Text _voteButtonLabel;

        public void Bind(PlayerData player, Action<PlayerData> onVoteClicked)
        {
            _nameLabel.text = player.DisplayName;

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_voting_vote_button", text => _voteButtonLabel.text = text));

            _voteButton.onClick.AddListener(() => onVoteClicked(player));
        }
    }
}