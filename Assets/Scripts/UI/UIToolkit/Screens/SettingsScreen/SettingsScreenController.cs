using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Bunker.Localization;

namespace Bunker.UI
{
    public class SettingsScreenController : ScreenController
    {
        private readonly DropdownField _languageDropdown;
        private readonly Toggle _soundToggle, _musicToggle;
        private readonly Button _backButton;
        private List<Locale> _availableLocales;

        public SettingsScreenController(VisualElement root) : base(root)
        {
            _languageDropdown = root.Q<DropdownField>("language-dropdown");
            _soundToggle = root.Q<Toggle>("sound-toggle");
            _musicToggle = root.Q<Toggle>("music-toggle");
            _backButton = root.Q<Button>("back-button");

            _backButton.clicked += () => UIManager.Instance.ShowScreen<MainMenuScreenController>();
            _languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            _soundToggle.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("bunker_sound", e.newValue ? 1 : 0));
            _musicToggle.RegisterValueChangedCallback(e => PlayerPrefs.SetInt("bunker_music", e.newValue ? 1 : 0));

            RefreshStaticText();
        }

        private async void RefreshStaticText()
        {
            root.Q<Label>("title-label").text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_settings_title");
            _backButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_settings_back");
        }

        protected override void OnShown()
        {
            _availableLocales = LocalizationSettings.AvailableLocales.Locales;
            _languageDropdown.choices = _availableLocales.ConvertAll(l => l.LocaleName);

            var current = LocalizedTextService.GetCurrentLocale();
            var index = _availableLocales.IndexOf(current);
            _languageDropdown.SetValueWithoutNotify(index >= 0 ? _availableLocales[index].LocaleName : null);

            _soundToggle.SetValueWithoutNotify(PlayerPrefs.GetInt("bunker_sound", 1) == 1);
            _musicToggle.SetValueWithoutNotify(PlayerPrefs.GetInt("bunker_music", 1) == 1);
        }

        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            var index = _availableLocales.FindIndex(l => l.LocaleName == evt.newValue);
            if (index < 0) return;

            var locale = _availableLocales[index];
            LocalizedTextService.SetLocale(locale);
            PlayerPrefsNames.SetSavedLocaleCode(locale.Identifier.Code);
        }
    }
}