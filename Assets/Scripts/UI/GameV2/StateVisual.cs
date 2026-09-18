using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bunker.UI.GameV2
{
    // The shared half of the pattern PlayerListItem introduced: a UI element's
    // appearance in each of its states lives in an inspector-configured array,
    // not a C# switch statement over BunkerTheme constants. Every state-driven
    // element implements this on its own per-state struct (one `state` field,
    // one `State` property) and looks itself up through StateVisual.Find —
    // that shared lookup is the only piece that would otherwise be copy-pasted
    // into every component (PlayerListItem, TraitSlotV2, RevealCategoryButton, ...).
    //
    // This stays a generic METHOD, never a generic MonoBehaviour field: each
    // component still declares its own concrete, ordinary array
    // (`TraitSlotVisual[]`, `PlayerStateVisual[]`, ...), which Unity serializes
    // and draws in the Inspector exactly as it always has. Nothing generic ever
    // reaches the Inspector or the player build's serialized data.
    public interface IStateVisual<TState> where TState : Enum
    {
        TState State { get; }
    }

    public static class StateVisual
    {
        public static TVisual Find<TState, TVisual>(TVisual[] entries, TState state, UnityEngine.Object context = null)
            where TState : Enum
            where TVisual : struct, IStateVisual<TState>
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (EqualityComparer<TState>.Default.Equals(entry.State, state)) return entry;
                }
            }

            Debug.LogWarning($"[{typeof(TVisual).Name}] No visual configured for state '{state}'" +
                (context != null ? $" on '{context.name}'" : string.Empty));
            return default;
        }
    }
}
