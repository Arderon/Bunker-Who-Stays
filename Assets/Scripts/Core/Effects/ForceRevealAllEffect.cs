namespace Bunker.Core.Effects
{
    // Forces the target to reveal one random still-hidden trait to everyone.
    public class ForceRevealAllEffect : ISpecialCardEffect
    {
        public bool CanApply(GameSession session, PlayerData caster, PlayerData target)
        {
            if (target == null || target.IsEliminated) return false;
            if (target.PlayerId == caster.PlayerId) return false;
            return target.HasUnrevealedTraits();
        }

        public void Apply(GameSession session, PlayerData caster, PlayerData target)
        {
            // Picking which hidden trait gets forcibly revealed and firing the
            // public OnTraitRevealed event is handled by GameSession, since
            // it already owns that event and the random selection.
        }
    }
}