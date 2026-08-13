using UnityEngine;
using UnityEngine.UI;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class TraitSlotView : MonoBehaviour
    {
        public CardCategory Category;

        [SerializeField] private Text _categoryLabel;
        [SerializeField] private Text _valueLabel;

        private void Awake()
        {
            var categoryKey = $"ui_game_category_{Category.ToString().ToLowerInvariant()}";
            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, categoryKey, text => _categoryLabel.text = text));
        }

        public void Refresh(CharacterTrait trait, bool isRevealed)
        {
            if (!isRevealed)
            {
                StartCoroutine(LocalizedTextService.GetTextCoroutine(
                    LocalizationTableNames.UI, "ui_game_hidden_trait", text => _valueLabel.text = text));
                return;
            }

            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.CardContent, trait.LocalizationKey, text => _valueLabel.text = text));
        }
    }
}