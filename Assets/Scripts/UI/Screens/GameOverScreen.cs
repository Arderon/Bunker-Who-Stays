using Bunker.Core;
using Bunker.Localization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class GameOverScreen : UIScreen
    {
        [SerializeField] private GameObject _survivorsBlock;
        [SerializeField] private TMP_Text _survivorsListLabel;
        [SerializeField] private GameObject _allEliminatedBlock;
        [SerializeField] private Button _playAgainButton;
        [SerializeField] private Button _backToMenuButton;

        private void Awake()
        {
            _backToMenuButton.onClick.AddListener(() =>
            {
                LobbyServiceLocator.Current.LeaveLobby();
                UIManager.Instance.ShowScreen<MainMenuScreen>();
            });

            _playAgainButton.onClick.AddListener(() => UIManager.Instance.ShowScreen<LobbyScreen>());
        }

        public void Bind(GameOverResult result)
        {
            bool hasSurvivors = result.EndReason == GameOverResult.Reason.SurvivorsTargetReached;

            _survivorsBlock.SetActive(hasSurvivors);
            _allEliminatedBlock.SetActive(!hasSurvivors);

            if (hasSurvivors)
            {
                var names = string.Join(", ", result.Survivors.Select(p => p.DisplayName));
                StartCoroutine(LocalizedTextService.GetTextCoroutine(
                    LocalizationTableNames.UI, "ui_gameover_survivors_list",
                    text => _survivorsListLabel.text = text, names));
            }
        }
    }
}