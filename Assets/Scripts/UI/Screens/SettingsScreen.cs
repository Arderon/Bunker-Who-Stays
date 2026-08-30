using Bunker.Localization;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class SettingsScreen : UIScreen
    {
        [SerializeField] private TMP_Dropdown _languageDropdown;
        [SerializeField] private Toggle _soundToggle;
        [SerializeField] private Toggle _musicToggle;
        [SerializeField] private Button _backButton;

        private List<Locale> _availableLocales;

        private void Awake()
        {
            _backButton.onClick.AddListener(() => UIManager.Instance.ShowScreen<MainMenuScreen>());
            _languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            _soundToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt("bunker_sound", v ? 1 : 0));
            _musicToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt("bunker_music", v ? 1 : 0));
        }

        protected override void OnShown()
        {
            _availableLocales = LocalizationSettings.AvailableLocales.Locales;

            _languageDropdown.ClearOptions();
            _languageDropdown.AddOptions(_availableLocales.ConvertAll(l => l.LocaleName));

            var current = LocalizedTextService.GetCurrentLocale();
            _languageDropdown.value = _availableLocales.IndexOf(current);

            _soundToggle.isOn = PlayerPrefs.GetInt("bunker_sound", 1) == 1;
            _musicToggle.isOn = PlayerPrefs.GetInt("bunker_music", 1) == 1;
        }

        private void OnLanguageChanged(int index)
        {
            var locale = _availableLocales[index];
            LocalizedTextService.SetLocale(locale);
            PlayerPrefsNames.SetSavedLocaleCode(locale.Identifier.Code);
        }
    }
}