using System;
using System.Linq;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class SpecialCardModalController
    {
        private readonly VisualElement _overlayRoot;
        private readonly VisualElement _targetListContainer, _categoryListContainer;
        private readonly Button _confirmButton;
        private readonly Label _errorLabel;

        private GameSession _session;
        private string _localPlayerId, _selectedTargetId;
        private CardCategory? _selectedCategory;

        public SpecialCardModalController(VisualElement overlayRoot, VisualElement content)
        {
            _overlayRoot = overlayRoot;
            _targetListContainer = content.Q<VisualElement>("target-list-container");
            _categoryListContainer = content.Q<VisualElement>("category-list-container");
            _confirmButton = content.Q<Button>("confirm-button");
            _errorLabel = content.Q<Label>("error-label");

            _confirmButton.clicked += OnConfirmClicked;
            _overlayRoot.AddToClassList("hidden");

            RefreshStaticText();
        }

        private async void RefreshStaticText()
        {
            _confirmButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_special_card_confirm");
        }

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;
        }

        public void Open()
        {
            _errorLabel.AddToClassList("hidden");
            _selectedTargetId = null;
            _selectedCategory = null;

            var player = _session.GetPlayer(_localPlayerId);
            bool isSwap = player.Special?.EffectType == SpecialCardEffectType.SwapTrait;
            _categoryListContainer.EnableInClassList("hidden", !isSwap);

            PopulateTargets();
            if (isSwap) PopulateCategories();

            _overlayRoot.RemoveFromClassList("hidden");
        }

        private void PopulateTargets()
        {
            _targetListContainer.Clear();
            foreach (var candidate in _session.ActivePlayers().Where(p => p.PlayerId != _localPlayerId))
            {
                var button = new Button { text = candidate.DisplayName };
                button.clicked += () => _selectedTargetId = candidate.PlayerId;
                _targetListContainer.Add(button);
            }
        }

        private void PopulateCategories()
        {
            _categoryListContainer.Clear();
            foreach (CardCategory category in Enum.GetValues(typeof(CardCategory)))
            {
                var button = new Button();
                _categoryListContainer.Add(button);
                RefreshCategoryButtonLabel(button, category);
                button.clicked += () => _selectedCategory = category;
            }
        }

        private async void RefreshCategoryButtonLabel(Button button, CardCategory category)
        {
            var key = $"ui_game_category_{category.ToString().ToLowerInvariant()}";
            button.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
        }

        private void OnConfirmClicked()
        {
            if (_selectedTargetId == null) { ShowError("ui_special_card_error_invalid_target"); return; }

            var player = _session.GetPlayer(_localPlayerId);
            SpecialCardEffectResult result;

            if (player.Special?.EffectType == SpecialCardEffectType.SwapTrait)
            {
                if (_selectedCategory == null) { ShowError("ui_special_card_error_invalid_target"); return; }
                result = _session.UseSwapTraitSpecialCard(_localPlayerId, _selectedTargetId, _selectedCategory.Value);
            }
            else
            {
                result = _session.UseSpecialCard(_localPlayerId, _selectedTargetId);
            }

            if (!result.Success) { ShowError("ui_special_card_error_already_used"); return; }
            _overlayRoot.AddToClassList("hidden");
        }

        private async void ShowError(string key)
        {
            _errorLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
            _errorLabel.RemoveFromClassList("hidden");
        }
    }
}