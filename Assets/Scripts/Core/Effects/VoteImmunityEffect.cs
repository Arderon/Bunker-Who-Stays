namespace Bunker.Core.Effects
{
    // Grants the caster immunity from being eliminated by this round's vote.
    // Self-targeted only.
    public class VoteImmunityEffect : ISpecialCardEffect
    {
        public bool CanApply(GameSession session, PlayerData caster, PlayerData target)
        {
            return session.Phase == GamePhase.Reveal || session.Phase == GamePhase.Discussion;
            // Must be used before voting starts — using it during/after Voting
            // would be unfair since results might already be known.
        }

        public void Apply(GameSession session, PlayerData caster, PlayerData target)
        {
            caster.HasVoteImmunityThisRound = true;
        }
    }
}