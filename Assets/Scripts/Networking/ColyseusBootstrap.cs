using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Bunker.Localization;
using Bunker.UI;

public class ColyseusBootstrap : MonoBehaviour
{
    [SerializeField] private string serverUrl = "ws://130.61.95.172:2567";

    // Awake() order between MonoBehaviours is not guaranteed — UIManager.Instance
    // is only set in UIManager's own Awake(), and this depends on it. Unity does
    // guarantee every Awake() on the scene finishes before any Start(), so
    // running from Start() instead makes that dependency safe without relying on
    // a Script Execution Order setting in the editor.
    private async void Start()
    {
        LocalizedTextService.Initialize();
        UIManager.Instance.Overlay.ShowLoading(true);

        try
        {
            // Authentication kept solely for a stable PlayerId (stage-0
            // decision) — Lobby and Relay are no longer used at all.
            await UnityServices.InitializeAsync();

            // Anonymous auth caches its PlayerId under a per-machine PlayerPrefs
            // key shared by every launch of the same build, so running several
            // copies of the .exe side by side to test multiplayer would
            // otherwise sign them all in as the same player. Passing
            // "-authProfile <name>" isolates each launch into its own cached
            // identity — see https://docs.unity.com/ugs/manual/authentication/manual/player-profiles.
            string authProfile = GetCommandLineArg("-authProfile");
            if (!string.IsNullOrEmpty(authProfile) && !AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SwitchProfile(authProfile);
            }

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

    private static string GetCommandLineArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }
}
