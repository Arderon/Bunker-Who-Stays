using System;
using System.Collections.Generic;
using System.Linq;
using Bunker.Core;
using UnityEngine;

namespace Bunker.Content
{
    // Turns designer-authored ScriptableObject pools into runtime PlayerData traits.
    // Pure C# logic (no MonoBehaviour) so it can be unit-tested and reused headlessly.
    public class CharacterCardGenerator
    {
        private readonly List<TraitPoolSO> _traitPools;
        private readonly SpecialCardPoolSO _specialCardPool;
        private readonly System.Random _random;

        // A seedable Random is injected so game sessions/tests can reproduce
        // the exact same deal by reusing the same seed.
        public CharacterCardGenerator(
            List<TraitPoolSO> traitPools,
            SpecialCardPoolSO specialCardPool,
            int? seed = null)
        {
            _traitPools = traitPools;
            _specialCardPool = specialCardPool;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            ValidatePools();
        }

        private void ValidatePools()
        {
            foreach (var pool in _traitPools)
            {
                if (!pool.IsValid())
                {
                    Debug.LogError($"[CharacterCardGenerator] Trait pool '{pool.name}' is invalid or empty.");
                }
            }

            if (_specialCardPool == null || _specialCardPool.Entries.Count == 0)
            {
                Debug.LogError("[CharacterCardGenerator] Special card pool is empty.");
            }
        }

        // Deals traits + a special card to every player for a fresh game session.
        // withinGameNoRepeat: if true, no two players get the exact same entry
        // for the same category within a single game (e.g. two "Surgeon" professions).
        public void DealToPlayers(List<PlayerData> players, bool withinGameNoRepeat = true)
        {
            // Track already-used entry ids per category to enforce no-repeat rule.
            var usedIdsPerCategory = new Dictionary<CardCategory, HashSet<string>>();

            foreach (var pool in _traitPools)
            {
                usedIdsPerCategory[pool.Category] = new HashSet<string>();
            }

            foreach (var player in players)
            {
                var traits = new List<CharacterTrait>();

                foreach (var pool in _traitPools)
                {
                    var entry = PickEntry(pool, usedIdsPerCategory[pool.Category], withinGameNoRepeat);

                    if (entry == null)
                    {
                        // Pool exhausted (more players than unique entries available).
                        // Fall back to allowing repeats rather than crashing the deal.
                        Debug.LogWarning($"[CharacterCardGenerator] Pool '{pool.Category}' exhausted, allowing repeat.");
                        entry = PickEntry(pool, usedIdsPerCategory[pool.Category], allowNoRepeat: false);
                    }

                    usedIdsPerCategory[pool.Category].Add(entry.Id);
                    traits.Add(new CharacterTrait(entry.Id, entry.Category, entry.LocalizationKey));
                }

                player.AssignTraits(traits);
                player.Special = PickSpecialCard();
            }
        }

        private TraitEntrySO PickEntry(TraitPoolSO pool, HashSet<string> usedIds, bool allowNoRepeat)
        {
            var candidates = allowNoRepeat
                ? pool.Entries.Where(e => !usedIds.Contains(e.Id)).ToList()
                : pool.Entries;

            if (candidates.Count == 0) return null;

            return WeightedRandomPick(candidates);
        }

        // Weighted random selection: entries with higher Weight are more likely to be picked.
        private TraitEntrySO WeightedRandomPick(IReadOnlyList<TraitEntrySO> candidates)
        {
            int totalWeight = candidates.Sum(e => Mathf.Max(1, e.Weight));
            int roll = _random.Next(0, totalWeight);

            int cumulative = 0;
            foreach (var entry in candidates)
            {
                cumulative += Mathf.Max(1, entry.Weight);
                if (roll < cumulative) return entry;
            }

            // Fallback, should not normally be reached.
            return candidates[candidates.Count - 1];
        }

        private SpecialCard PickSpecialCard()
        {
            var entries = _specialCardPool.Entries;
            var picked = entries[_random.Next(entries.Count)];
            return new SpecialCard(picked.Id, picked.EffectType, picked.LocalizationKey);
        }
    }
}