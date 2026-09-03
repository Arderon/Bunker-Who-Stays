using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Bunker.Content;
using Bunker.Localization;
using Bunker.UI;

public class UgsBootstrap : MonoBehaviour
{
    [SerializeField] private List<TraitPoolSO> _traitPools;
    [SerializeField] private SpecialCardPoolSO _specialCardPool;

    private async void Awake()
    {
        LocalizedTextService.Initialize();

        UIManager.Instance.Overlay.ShowLoading(true);

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log($"[UgsBootstrap] Signed in as {AuthenticationService.Instance.PlayerId}");

            LobbyServiceLocator.Current = new UgsLobbyService(_traitPools, _specialCardPool);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UgsBootstrap] Initialization failed: {ex}");
            UIManager.Instance.Overlay.ShowToast("ui_common_error_generic");
        }
        finally
        {
            UIManager.Instance.Overlay.ShowLoading(false);
        }
    }
}