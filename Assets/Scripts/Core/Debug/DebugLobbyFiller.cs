using UnityEngine;
using Bunker.UI;

// TEMPORARY debug tool for UI testing without real multiplayer.
// Remove or wrap in #if UNITY_EDITOR before shipping.
public class DebugLobbyFiller : MonoBehaviour
{
    [SerializeField]
    private string[] _fakeNames =
    {
        "Олена", "Максим", "Ірина", "Богдан", "Настя", "Віктор", "Тетяна"
    };

    [ContextMenu("Fill With Fake Players")]
    public void FillWithFakePlayers()
    {
        if (LobbyServiceLocator.Current is not LocalLobbyService local)
        {
            Debug.LogWarning("Current lobby service is not LocalLobbyService.");
            return;
        }

        foreach (var name in _fakeNames)
        {
            local.AddFakePlayer(name);
        }
    }
}