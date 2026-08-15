using System;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class RevealPanelController
    {
        public VisualElement Root { get; }

        private readonly Label _turnIndicatorLabel;
        private readonly Button _startDiscussionButton;
        private readonly Button[] _categoryButtons = new Button[7];
        private GameSession _session;
        private string _localPlayerId;

        public RevealPanelController(VisualElement root)
        {
            Root = root;
            _turnIndicatorLabel = root.Q<Label>("turn-indicator-label");
            _startDiscussionButton = root.Q<Button>("start-discussion-button");
            var container = root.Q<VisualElement>("category-buttons-container");

            foreach (CardCategory category in Enum.GetValues(typeof(CardCategory)))
            {
                var button = new Button();
                container.Add(button);
                _categoryButtons[(int)category] = button;
                RefreshButtonLabel(button, category);
            }

            _startDiscussionButton.clicked += () => _session.StartDiscussionPhase(durationSeconds: 120);
        }

        private async void RefreshButtonLabel(Button button, CardCategory category)
        {
            var key = $"ui_game_category_{category.ToString().ToLowerInvariant()}";
            button.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
        }

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;

            session.OnRoundStarted += _ => Refresh();
            session.OnTraitRevealed += (_, _) => Refresh();
            session.OnRevealPassCompleted += () => _startDiscussionButton.style.display = DisplayStyle.Flex;

            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                var category = (CardCategory)i;
                _categoryButtons[i].clicked += () => _session.RevealNextTrait(_localPlayerId, category);
            }

            _startDiscussionButton.style.display = DisplayStyle.None;
            Refresh();
        }

        private async void Refresh()
        {
            bool isMyTurn = _session.CurrentTurnPlayerId == _localPlayerId;
            var currentPlayer = _session.GetPlayer(_session.CurrentTurnPlayerId);

            _turnIndicatorLabel.text = isMyTurn
                ? await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_game_your_turn")
                : await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_game_waiting_for_player", currentPlayer?.DisplayName);

            var localPlayer = _session.GetPlayer(_localPlayerId);
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                var category = (CardCategory)i;
                _categoryButtons[i].SetEnabled(isMyTurn && !localPlayer.IsCategoryRevealed(category));
            }
        }
    }
}