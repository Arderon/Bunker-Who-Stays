using System;
using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class MyCardPanelController
    {
        public VisualElement Root { get; }
        public event Action SpecialCardRequested;

        private readonly Button _specialCardButton;
        private readonly TraitSlotController[] _slots = new TraitSlotController[7];
        private GameSession _session;
        private string _localPlayerId;

        public MyCardPanelController(VisualElement root, VisualTreeAsset traitSlotAsset)
        {
            Root = root;
            var container = root.Q<VisualElement>("trait-slots-container");
            _specialCardButton = root.Q<Button>("special-card-button");

            foreach (CardCategory category in Enum.GetValues(typeof(CardCategory)))
            {
                var element = traitSlotAsset.CloneTree();
                container.Add(element);
                _slots[(int)category] = new TraitSlotController(element, category);
            }

            _specialCardButton.clicked += () => SpecialCardRequested?.Invoke();
        }

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;

            session.OnTraitRevealed += (p, _) => { if (p.PlayerId == _localPlayerId) RefreshAllSlots(); };
            session.OnSpecialCardUsed += (p, _) => { if (p.PlayerId == _localPlayerId) RefreshSpecialCardButton(); };

            RefreshAllSlots();
            RefreshSpecialCardButton();
        }

        private void RefreshAllSlots()
        {
            var player = _session.GetPlayer(_localPlayerId);
            foreach (var slot in _slots)
                slot.Refresh(player.GetTrait(slot.Category), player.IsCategoryRevealed(slot.Category));
        }

        private async void RefreshSpecialCardButton()
        {
            var player = _session.GetPlayer(_localPlayerId);
            _specialCardButton.SetEnabled(!player.HasUsedSpecialCard);
            var key = player.HasUsedSpecialCard ? "ui_special_card_used" : "ui_special_card_use";
            _specialCardButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
        }
    }
}