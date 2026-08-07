using System;

namespace Bunker.Core
{
    public static class SpecialCardEffectType
    {
        public const string RevealHiddenTrait = "RevealHiddenTrait";
        public const string VoteImmunity = "VoteImmunity";
        public const string SwapTrait = "SwapTrait";
        public const string ForceRevealAll = "ForceRevealAll";
    }

    [Serializable]
    public class SpecialCard
    {
        public string Id;
        public string EffectType;
        public string LocalizationKey;

        public SpecialCard(string id, string effectType, string localizationKey)
        {
            Id = id;
            EffectType = effectType;
            LocalizationKey = localizationKey;
        }
    }
}