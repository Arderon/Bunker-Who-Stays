using System.Collections.Generic;
using UnityEngine;
using Bunker.Content;
using Bunker.Core;

public static class TestContentFactory
{
    public static List<TraitPoolSO> CreateDefaultTraitPools()
    {
        var pools = new List<TraitPoolSO>();

        foreach (CardCategory category in System.Enum.GetValues(typeof(CardCategory)))
        {
            var pool = ScriptableObject.CreateInstance<TraitPoolSO>();
            pool.Category = category;
            pool.Entries = new List<TraitEntrySO>();

            for (int i = 0; i < 10; i++)
            {
                var entry = ScriptableObject.CreateInstance<TraitEntrySO>();
                entry.Id = $"{category}_{i}";
                entry.Category = category;
                entry.LocalizationKey = $"trait_{category}_{i}";
                entry.Weight = 1;
                pool.Entries.Add(entry);
            }

            pools.Add(pool);
        }

        return pools;
    }

    public static SpecialCardPoolSO CreateDefaultSpecialCardPool()
    {
        var pool = ScriptableObject.CreateInstance<SpecialCardPoolSO>();
        pool.Entries = new List<SpecialCardEntrySO>();

        string[] effectTypes =
        {
            SpecialCardEffectType.RevealHiddenTrait,
            SpecialCardEffectType.VoteImmunity,
            SpecialCardEffectType.SwapTrait,
            SpecialCardEffectType.ForceRevealAll
        };

        foreach (var effect in effectTypes)
        {
            var card = ScriptableObject.CreateInstance<SpecialCardEntrySO>();
            card.Id = $"special_{effect}";
            card.EffectType = effect;
            card.LocalizationKey = $"special_{effect}";
            pool.Entries.Add(card);
        }

        return pool;
    }
}