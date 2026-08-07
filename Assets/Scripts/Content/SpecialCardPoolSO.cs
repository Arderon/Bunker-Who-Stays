using System.Collections.Generic;
using UnityEngine;

namespace Bunker.Content
{
    // A single special card definition as a designer-editable asset.
    [CreateAssetMenu(menuName = "Bunker/Special Card Entry", fileName = "NewSpecialCard")]
    public class SpecialCardEntrySO : ScriptableObject
    {
        public string Id;

        [Tooltip("Must match one of the constants in SpecialCardEffectType")]
        public string EffectType;

        public string LocalizationKey;
    }

    // Pool of all special cards available in the game.
    [CreateAssetMenu(menuName = "Bunker/Special Card Pool", fileName = "SpecialCardPool")]
    public class SpecialCardPoolSO : ScriptableObject
    {
        public List<SpecialCardEntrySO> Entries = new();
    }
}