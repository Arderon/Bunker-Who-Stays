using UnityEngine;
using UnityEngine.UI;
using Bunker.Core;

namespace Bunker.UI
{
    public class MyCardPanel : MonoBehaviour
    {
        [SerializeField] private TraitSlotView[] _traitSlots; // one per CardCategory, ordered same as enum
        [SerializeField] private Button _specialCardButton;
        [SerializeField] private Text _specialCardButtonLabel;

        private GameSession _session;
        private string _localPlayerId;

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;

            session.OnTraitRevealed += OnTraitRevealed;
            session.OnSpecialCardUsed += OnSpecialCardUsed;

            _specialCardButton.onClick.AddListener(OnSpecialCardButtonClicked);

            RefreshAllSlots();
            RefreshSpecialCardButton();
        }

        private void RefreshAllSlots()
        {
            var player = _session.GetPlayer(_localPlayerId);
            foreach (var slot in _traitSlots)
            {
                var trait = player.GetTrait(slot.Category);
                bool revealed = player.IsCategoryRevealed(slot.Category);
                slot.Refresh(trait, revealed);
            }
        }

        private void OnTraitRevealed(PlayerData player, CharacterTrait trait)
        {
            if (player.PlayerId != _localPlayerId) return;
            RefreshAllSlots();
        }

        private void OnSpecialCardUsed(PlayerData player, SpecialCard card)
        {
            if (player.PlayerId != _localPlayerId) return;
            RefreshSpecialCardButton();
        }

        private void RefreshSpecialCardButton()
        {
            var player = _session.GetPlayer(_localPlayerId);
            _specialCardButton.interactable = !player.HasUsedSpecialCard;

            var key = player.HasUsedSpecialCard ? "ui_special_card_used" : "ui_special_card_use";
            StartCoroutine(Bunker.Localization.LocalizedTextService.GetTextCoroutine(
                Bunker.Localization.LocalizationTableNames.UI, key,
                text => _specialCardButtonLabel.text = text));
        }

        private void OnSpecialCardButtonClicked()
        {
            // Actual modal is opened by GameScreen's SpecialCardModal reference;
            // simplest wiring for MVP is a shared static event or direct reference
            // passed in at Bind-time. Kept as a broadcast event to avoid a hard
            // dependency between MyCardPanel and SpecialCardModal.
            SpecialCardModal.RequestOpen?.Invoke();
        }
    }
}