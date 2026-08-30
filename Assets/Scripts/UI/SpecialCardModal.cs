using Bunker.Core;
using Bunker.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class SpecialCardModal : MonoBehaviour
    {
        // Simple decoupling point so MyCardPanel can request the modal open
        // without holding a direct reference to it.
        public static Action RequestOpen;

        [SerializeField] private GameObject _root;
        [SerializeField] private Transform _targetListContainer;
        [SerializeField] private Button _targetButtonPrefab;
        [SerializeField] private Transform _categoryListContainer;
        [SerializeField] private Button _categoryButtonPrefab;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _errorLabel;

        private GameSession _session;
        private string _localPlayerId;
        private string _selectedTargetId;
        private CardCategory? _selectedCategory;

        public void Bind(GameSession session, string localPlayerId)
        {
            _session = session;
            _localPlayerId = localPlayerId;
            _root.SetActive(false);

            RequestOpen += Open;
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDestroy()
        {
            RequestOpen -= Open;
        }

        private void Open()
        {
            _errorLabel.gameObject.SetActive(false);
            _selectedTargetId = null;
            _selectedCategory = null;

            var player = _session.GetPlayer(_localPlayerId);
            bool isSwap = player.Special?.EffectType == SpecialCardEffectType.SwapTrait;
            _categoryListContainer.gameObject.SetActive(isSwap);

            PopulateTargets();
            if (isSwap) PopulateCategories();

            _root.SetActive(true);
        }

        private void PopulateTargets()
        {
            foreach (Transform child in _targetListContainer) Destroy(child.gameObject);

            var candidates = _session.ActivePlayers().Where(p => p.PlayerId != _localPlayerId);
            foreach (var candidate in candidates)
            {
                var button = Instantiate(_targetButtonPrefab, _targetListContainer);
                button.GetComponentInChildren<TMP_Text>().text = candidate.DisplayName;
                button.onClick.AddListener(() => _selectedTargetId = candidate.PlayerId);
            }
        }

        private void PopulateCategories()
        {
            foreach (Transform child in _categoryListContainer) Destroy(child.gameObject);

            foreach (CardCategory category in Enum.GetValues(typeof(CardCategory)))
            {
                var button = Instantiate(_categoryButtonPrefab, _categoryListContainer);
                var key = $"ui_game_category_{category.ToString().ToLowerInvariant()}";
                StartCoroutine(LocalizedTextService.GetTextCoroutine(
                    LocalizationTableNames.UI, key,
                    text => button.GetComponentInChildren<TMP_Text>().text = text));
                button.onClick.AddListener(() => _selectedCategory = category);
            }
        }

        private void OnConfirmClicked()
        {
            if (_selectedTargetId == null)
            {
                ShowError("ui_special_card_error_invalid_target");
                return;
            }

            var player = _session.GetPlayer(_localPlayerId);
            SpecialCardEffectResult result;

            if (player.Special?.EffectType == SpecialCardEffectType.SwapTrait)
            {
                if (_selectedCategory == null)
                {
                    ShowError("ui_special_card_error_invalid_target");
                    return;
                }
                result = _session.UseSwapTraitSpecialCard(_localPlayerId, _selectedTargetId, _selectedCategory.Value);
            }
            else
            {
                result = _session.UseSpecialCard(_localPlayerId, _selectedTargetId);
            }

            if (!result.Success)
            {
                ShowError("ui_special_card_error_already_used");
                return;
            }

            _root.SetActive(false);
        }

        private void ShowError(string key)
        {
            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, key, text =>
                {
                    _errorLabel.text = text;
                    _errorLabel.gameObject.SetActive(true);
                }));
        }
    }
}