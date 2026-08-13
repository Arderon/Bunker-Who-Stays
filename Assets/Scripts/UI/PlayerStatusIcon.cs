using UnityEngine;
using UnityEngine.UI;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class PlayerStatusIcon : MonoBehaviour
    {
        [SerializeField] private Text _nameLabel;
        [SerializeField] private GameObject _eliminatedTag;
        [SerializeField] private CanvasGroup _canvasGroup;

        public void Bind(PlayerData player)
        {
            _nameLabel.text = player.DisplayName;
            _eliminatedTag.SetActive(player.IsEliminated);
            _canvasGroup.alpha = player.IsEliminated ? 0.4f : 1f;

            if (player.IsEliminated)
            {
                StartCoroutine(LocalizedTextService.GetTextCoroutine(
                    LocalizationTableNames.UI, "ui_game_player_eliminated_tag",
                    text => _eliminatedTag.GetComponentInChildren<Text>().text = text));
            }
        }
    }
}