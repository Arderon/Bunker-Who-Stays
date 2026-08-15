using System.Linq;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class GameOverScreenController : ScreenController
    {
        private readonly VisualElement _survivorsBlock, _allEliminatedBlock;
        private readonly Label _titleLabel, _survivorsTitleLabel, _survivorsListLabel, _allEliminatedLabel;
        private readonly Button _playAgainButton, _backToMenuButton;

        public GameOverScreenController(VisualElement root) : base(root)
        {
            _survivorsBlock = root.Q<VisualElement>("survivors-block");
            _allEliminatedBlock = root.Q<VisualElement>("all-eliminated-block");
            _titleLabel = root.Q<Label>("title-label");
            _survivorsTitleLabel = root.Q<Label>("survivors-title-label");
            _survivorsListLabel = root.Q<Label>("survivors-list-label");
            _allEliminatedLabel = root.Q<Label>("all-eliminated-label");
            _playAgainButton = root.Q<Button>("play-again-button");
            _backToMenuButton = root.Q<Button>("back-to-menu-button");

            _playAgainButton.clicked += () => UIManager.Instance.ShowScreen<LobbyScreenController>();
            _backToMenuButton.clicked += () =>
            {
                LobbyServiceLocator.Current.LeaveLobby();
                UIManager.Instance.ShowScreen<MainMenuScreenController>();
            };

            RefreshStaticText();
        }

        private async void RefreshStaticText()
        {
            _titleLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_gameover_title");
            _survivorsTitleLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_gameover_survivors_title");
            _allEliminatedLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_gameover_all_eliminated");
            _playAgainButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_gameover_play_again");
            _backToMenuButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_gameover_back_to_menu");
        }

        public async void Bind(GameOverResult result)
        {
            bool hasSurvivors = result.EndReason == GameOverResult.Reason.SurvivorsTargetReached;
            _survivorsBlock.style.display = hasSurvivors ? DisplayStyle.Flex : DisplayStyle.None;
            _allEliminatedBlock.style.display = hasSurvivors ? DisplayStyle.None : DisplayStyle.Flex;

            if (hasSurvivors)
            {
                var names = string.Join(", ", result.Survivors.Select(p => p.DisplayName));
                _survivorsListLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_gameover_survivors_list", names);
            }
        }
    }
}