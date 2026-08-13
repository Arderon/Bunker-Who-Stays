using UnityEngine;

namespace Bunker.UI
{
    // Small helper around PlayerPrefs so key names aren't duplicated as
    // raw strings across the UI layer.
    public static class PlayerPrefsNames
    {
        private const string DisplayNameKey = "bunker_display_name";
        private const string LocaleKey = "bunker_locale_code";

        public static string GetLocalDisplayName()
        {
            return PlayerPrefs.GetString(DisplayNameKey, "Player");
        }

        public static void SetLocalDisplayName(string name)
        {
            PlayerPrefs.SetString(DisplayNameKey, name);
        }

        public static string GetSavedLocaleCode() => PlayerPrefs.GetString(LocaleKey, null);

        public static void SetSavedLocaleCode(string code) => PlayerPrefs.SetString(LocaleKey, code);
    }
}