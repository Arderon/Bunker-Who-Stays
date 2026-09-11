using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Bunker.Localization;
using Bunker.UI;

public class ColyseusBootstrap : MonoBehaviour
{
    [SerializeField] private string serverUrl = "ws://130.61.95.172:2567";

    private async void Awake()
    {
        LocalizedTextService.Initialize();
        UIManager.Instance.Overlay.ShowLoading(true);

        try
        {
            // Authentication kept solely for a stable PlayerId (stage-0
            // decision) — Lobby and Relay are no longer used at all.
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            LobbyServiceLocator.Current = new ColyseusLobbyService(serverUrl);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ColyseusBootstrap] Initialization failed: {ex}");
            UIManager.Instance.Overlay.ShowToast("ui_common_error_generic");
        }
        finally
        {
            UIManager.Instance.Overlay.ShowLoading(false);
        }
    }
}
