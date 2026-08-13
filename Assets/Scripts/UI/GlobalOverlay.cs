using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Bunker.Localization;

namespace Bunker.UI
{
    // Cross-screen overlay for loading state, connection issues, and
    // short-lived toast messages. Lives on its own always-on-top Canvas.
    public class GlobalOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject _loadingSpinner;
        [SerializeField] private GameObject _connectionLostBanner;
        [SerializeField] private Text _connectionLostText;
        [SerializeField] private GameObject _toastRoot;
        [SerializeField] private Text _toastText;
        [SerializeField] private float _toastDurationSeconds = 2.5f;

        private Coroutine _toastRoutine;

        public void ShowLoading(bool show)
        {
            _loadingSpinner.SetActive(show);
        }

        public void ShowConnectionLost(bool reconnecting)
        {
            _connectionLostBanner.SetActive(true);
            var key = reconnecting ? "ui_common_reconnecting" : "ui_common_connection_lost";
            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, key, text => _connectionLostText.text = text));
        }

        public void HideConnectionLost()
        {
            _connectionLostBanner.SetActive(false);
        }

        // Shows a short localized toast (e.g. generic error). key must exist
        // in the UI table. Pass Smart Format arguments if the string needs them.
        public void ShowToast(string key, params object[] arguments)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ShowToastRoutine(key, arguments));
        }

        private IEnumerator ShowToastRoutine(string key, object[] arguments)
        {
            yield return LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, key, text => _toastText.text = text, arguments);

            _toastRoot.SetActive(true);
            yield return new WaitForSeconds(_toastDurationSeconds);
            _toastRoot.SetActive(false);
        }
    }
}