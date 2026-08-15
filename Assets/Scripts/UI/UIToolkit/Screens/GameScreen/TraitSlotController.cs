using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class TraitSlotController
    {
        public CardCategory Category { get; }
        private readonly Label _categoryLabel, _valueLabel;

        public TraitSlotController(VisualElement root, CardCategory category)
        {
            Category = category;
            _categoryLabel = root.Q<Label>("category-label");
            _valueLabel = root.Q<Label>("value-label");
            RefreshCategoryLabel();
        }

        private async void RefreshCategoryLabel()
        {
            var key = $"ui_game_category_{Category.ToString().ToLowerInvariant()}";
            _categoryLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
        }

        public async void Refresh(CharacterTrait trait, bool isRevealed)
        {
            _valueLabel.text = isRevealed
                ? await LocalizedTextService.GetTextAsync(LocalizationTableNames.CardContent, trait.LocalizationKey)
                : await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_game_hidden_trait");
        }
    }
}