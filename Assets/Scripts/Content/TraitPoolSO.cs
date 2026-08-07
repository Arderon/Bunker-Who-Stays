using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bunker.Content
{
    // A pool of all possible trait entries for a single category.
    // One asset per category (e.g. "ProfessionPool", "HobbyPool").
    [CreateAssetMenu(menuName = "Bunker/Trait Pool", fileName = "NewTraitPool")]
    public class TraitPoolSO : ScriptableObject
    {
        public Bunker.Core.CardCategory Category;
        public List<TraitEntrySO> Entries = new();

        public bool IsValid()
        {
            // Basic sanity checks to catch content mistakes early, before runtime.
            if (Entries == null || Entries.Count == 0) return false;
            return Entries.All(e => e.Category == Category && !string.IsNullOrEmpty(e.Id));
        }
    }
}