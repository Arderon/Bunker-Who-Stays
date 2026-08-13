using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField] private Text _displayNameLabel;
        [SerializeField] private GameObject _hostTag;
        [SerializeField] private Text _readyStatusLabel;

        public void Bind(LobbyPlayerInfo player)
        {
            _displayNameLabel.text = player.DisplayName;
            _hostTag.SetActive(player.IsHost);

            var key = player.IsReady ? "ui_lobby_player_ready" : "ui_lobby_player_not_ready";
            StartCoroutine(Bunker.Localization.LocalizedTextService.GetTextCoroutine(
                Bunker.Localization.LocalizationTableNames.UI, key,
                text => _readyStatusLabel.text = text));
        }
    }
}