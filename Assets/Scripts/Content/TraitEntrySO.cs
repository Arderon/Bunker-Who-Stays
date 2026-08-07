using UnityEngine;

namespace Bunker.Content
{
    // A single trait option living in a content pool (e.g. one specific profession).
    // Kept as a ScriptableObject so designers can create/edit entries in the Unity Editor
    // without touching code.
    [CreateAssetMenu(menuName = "Bunker/Trait Entry", fileName = "NewTraitEntry")]
    public class TraitEntrySO : ScriptableObject
    {
        [Tooltip("Unique id within its category, e.g. 'profession_surgeon'")]
        public string Id;

        public Bunker.Core.CardCategory Category;

        [Tooltip("Key used to look up the localized text in the Unity Localization String Table")]
        public string LocalizationKey;

        [Tooltip("Optional weight for weighted random selection. Default 1 = normal chance.")]
        public int Weight = 1;

        [Tooltip("Optional tags for future balance rules (e.g. 'physical', 'elderly-incompatible')")]
        public string[] Tags;
    }
}