using System;
using System.Collections.Generic;
using Bunker.Core.Effects;

namespace Bunker.Core
{
    // Maps a SpecialCard's string EffectType to its concrete implementation.
    // Using a string key (matching SpecialCardEffectType constants) instead of
    // an enum lets designers add new cards as data without a code change,
    // as long as a matching effect class is registered here.
    public class SpecialCardEffectRegistry
    {
        private readonly Dictionary<string, ISpecialCardEffect> _effects = new();

        public SpecialCardEffectRegistry()
        {
            Register(SpecialCardEffectType.RevealHiddenTrait, new RevealHiddenTraitEffect());
            Register(SpecialCardEffectType.VoteImmunity, new VoteImmunityEffect());
            Register(SpecialCardEffectType.SwapTrait, new SwapTraitEffect());
            Register(SpecialCardEffectType.ForceRevealAll, new ForceRevealAllEffect());
        }

        private void Register(string effectType, ISpecialCardEffect effect)
        {
            _effects[effectType] = effect;
        }

        public bool TryGet(string effectType, out ISpecialCardEffect effect)
        {
            return _effects.TryGetValue(effectType, out effect);
        }
    }
}