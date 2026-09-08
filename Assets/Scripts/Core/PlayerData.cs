using System;
using System.Collections.Generic;
using System.Linq;

namespace Bunker.Core
{
    [Serializable]
    public class PlayerData
    {
        public string PlayerId;
        public string DisplayName;

        public List<CharacterTrait> Traits { get; private set; } = new();
        public SpecialCard Special;

        public HashSet<CardCategory> RevealedCategories { get; private set; } = new();

        public bool IsEliminated;
        public bool HasUsedSpecialCard;
        public bool HasVoteImmunityThisRound;

        public PlayerData(string playerId, string displayName)
        {
            PlayerId = playerId;
            DisplayName = displayName;
        }

        public void AssignTraits(IEnumerable<CharacterTrait> traits)
        {
            Traits = traits.ToList();
        }

        public CharacterTrait GetTrait(CardCategory category)
        {
            return Traits.FirstOrDefault(t => t.Category == category);
        }

        public void ReplaceTrait(CardCategory category, CharacterTrait newTrait)
        {
            var index = Traits.FindIndex(t => t.Category == category);
            if (index >= 0)
            {
                Traits[index] = newTrait;
            }
        }

        // Adds a trait for this category if none exists yet, or replaces the
        // existing one. Used by ColyseusGameSessionController to apply
        // incoming per-trait content that arrives via messages (dealtHand,
        // traitRevealed, traitUpdated) rather than all at once via
        // AssignTraits — the network client builds its picture of each
        // player's hand incrementally as these messages arrive.
        public void ReplaceOrAddTrait(CardCategory category, CharacterTrait trait)
        {
            var index = Traits.FindIndex(t => t.Category == category);
            if (index >= 0) Traits[index] = trait;
            else Traits.Add(trait);
        }

        public bool IsCategoryRevealed(CardCategory category)
        {
            return RevealedCategories.Contains(category);
        }

        public void RevealCategory(CardCategory category)
        {
            RevealedCategories.Add(category);
        }

        public bool HasUnrevealedTraits()
        {
            return RevealedCategories.Count < Traits.Count;
        }

        public void ResetPerRoundFlags()
        {
            HasVoteImmunityThisRound = false;
        }
    }
}
