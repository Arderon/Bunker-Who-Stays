using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Bunker.Localization
{
    // Single access point for retrieving localized text at runtime.
    // Wraps the Unity Localization API so the rest of the codebase (UI,
    // game logic messages) never calls LocalizationSettings directly.
    public static class LocalizedTextService
    {
        // Fired whenever the active locale changes, so subscribed UI can
        // refresh already-displayed text without a full screen reload.
        public static event Action OnLocaleChanged;

        private static bool _isSubscribed;

        // Call once at app startup (e.g. from a bootstrap script) to start
        // forwarding Unity's locale-changed event through our own event.
        public static void Initialize()
        {
            if (_isSubscribed) return;

            LocalizationSettings.SelectedLocaleChanged += _ => OnLocaleChanged?.Invoke();
            _isSubscribed = true;
        }

        // --- Async retrieval (preferred, non-blocking) ----------------------

        // Retrieves localized text with optional Smart Format arguments
        // (e.g. "Round {0}" with arguments: 3). Awaitable from async methods.
        public static async Task<string> GetTextAsync(string table, string key, params object[] arguments)
        {
            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key, arguments);
            return await handle.Task;
        }

        // Coroutine variant for call sites that aren't async (most MonoBehaviour UI code).
        public static IEnumerator GetTextCoroutine(string table, string key, Action<string> onComplete, params object[] arguments)
        {
            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key, arguments);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                onComplete?.Invoke(handle.Result);
            }
            else
            {
                Debug.LogError($"[LocalizedTextService] Failed to load '{key}' from '{table}'.");
                onComplete?.Invoke(key); // fallback: show the raw key so missing text is obvious, not blank
            }
        }

        // --- Synchronous retrieval (use sparingly) --------------------------

        // Blocks until the string is loaded. Convenient for Editor tools,
        // debug logging, or code that genuinely cannot wait a frame.
        // Avoid calling this every frame or in hot paths — it can stall
        // if the underlying table asset isn't loaded yet (e.g. first call
        // after a locale switch, before Addressables finished loading).
        public static string GetTextImmediate(string table, string key, params object[] arguments)
        {
            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key, arguments);
            return handle.WaitForCompletion();
        }

        // --- Locale switching -------------------------------------------------

        public static void SetLocale(Locale locale)
        {
            LocalizationSettings.SelectedLocale = locale;
        }

        public static Locale GetCurrentLocale()
        {
            return LocalizationSettings.SelectedLocale;
        }
    }
}