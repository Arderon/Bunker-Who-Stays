using UnityEngine;
using Bunker.Content;
using Bunker.Localization;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private System.Collections.Generic.List<TraitPoolSO> _traitPools;
    [SerializeField] private SpecialCardPoolSO _specialCardPool;

    private void Awake()
    {
        LocalizedTextService.Initialize();
        Bunker.UI.LobbyServiceLocator.Current = new Bunker.UI.LocalLobbyService(_traitPools, _specialCardPool);
    }
}