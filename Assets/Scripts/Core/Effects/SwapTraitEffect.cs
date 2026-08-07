using System.Linq;

namespace Bunker.Core.Effects
{
    // Swaps one still-hidden trait category between the caster and the target.
    // Both players must have that category unrevealed, so the swap stays
    // invisible to everyone else until either of them chooses to reveal it.
    public class SwapTraitEffect : ISpecialCardEffect
    {
        public bool CanApply(GameSession session, PlayerData caster, PlayerData target)
        {
            if (target == null || target.IsEliminated) return false;
            if (target.PlayerId == caster.PlayerId) return false;

            // At least one category must be hidden for both players.
            return caster.Traits.Any(t =>
                !caster.IsCategoryRevealed(t.Category) &&
                !target.IsCategoryRevealed(t.Category));
        }

        // Category to swap is chosen by the caster; passed in via GameSession
        // as an extra parameter (see GameSession.UseSpecialCard overload below),
        // since ISpecialCardEffect.Apply's signature is intentionally generic.
        public void Apply(GameSession session, PlayerData caster, PlayerData target)
        {
            // Concrete swap logic lives in GameSession.UseSpecialCard because it
            // needs the extra "which category" parameter. This method exists
            // to satisfy the interface and is intentionally left as a no-op here.
        }
    }
}