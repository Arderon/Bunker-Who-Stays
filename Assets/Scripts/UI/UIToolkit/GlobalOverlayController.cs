using UnityEngine.UIElements;
using Bunker.Localization;

namespace Bunker.UI
{
    public class GlobalOverlayController
    {
        public VisualElement Root { get; }
        private readonly VisualElement _loadingSpinner, _connectionLostBanner, _toastRoot;
        private readonly Label _connectionLostText, _toastText;
        private IVisualElementScheduledItem _toastHideTask;

        public GlobalOverlayController(VisualElement root)
        {
            Root = root;
            _loadingSpinner = root.Q<VisualElement>("loading-spinner");
            _connectionLostBanner = root.Q<VisualElement>("connection-lost-banner");
            _connectionLostText = root.Q<Label>("connection-lost-text");
            _toastRoot = root.Q<VisualElement>("toast-root");
            _toastText = root.Q<Label>("toast-text");
        }

        public void ShowLoading(bool show) => _loadingSpinner.EnableInClassList("hidden", !show);

        public async void ShowConnectionLost(bool reconnecting)
        {
            _connectionLostBanner.RemoveFromClassList("hidden");
            var key = reconnecting ? "ui_common_reconnecting" : "ui_common_connection_lost";
            _connectionLostText.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key);
        }

        public void HideConnectionLost() => _connectionLostBanner.AddToClassList("hidden");

        public async void ShowToast(string key, params object[] arguments)
        {
            _toastText.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, key, arguments);
            _toastRoot.RemoveFromClassList("hidden");

            _toastHideTask?.Pause();
            _toastHideTask = Root.schedule.Execute(() => _toastRoot.AddToClassList("hidden")).ExecuteLater(2500);
        }
    }
}