using System.Linq;

namespace Bunker.Core.Effects
{
    // Lets the caster privately see one of the target's still-hidden traits,
    // without revealing it to anyone else.
    public class RevealHiddenTraitEffect : ISpecialCardEffect
    {
        public bool CanApply(GameSession session, PlayerData caster, PlayerData target)
        {
            if (target == null || target.IsEliminated) return false;
            if (target.PlayerId == caster.PlayerId) return false; // must target someone else
            return target.Traits.Any(t => !target.IsCategoryRevealed(t.Category));
        }

        public void Apply(GameSession session, PlayerData caster, PlayerData target)
        {
            // Actual trait pick and result delivery is handled by GameSession,
            // since it needs to pick a specific hidden category and route the
            // result privately to the caster only. See GameSession.UseSpecialCard.
        }
    }
}