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
        public void ReplaceTrait(CardCategory category, CharacterTrait newTrait)
        {
            var index = Traits.FindIndex(t => t.Category == category);
            if (index >= 0)
            {
                Traits[index] = newTrait;
            }
        }
    }
}