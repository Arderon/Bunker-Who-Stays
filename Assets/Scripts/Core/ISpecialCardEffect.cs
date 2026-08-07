namespace Bunker.Core
{
    // Contract for a special card effect. Each concrete effect is a small,
    // isolated piece of logic that mutates the game state in a specific way.
    // Kept side-effect-free besides mutating the passed session/players,
    // so effects stay easy to unit test individually.
    public interface ISpecialCardEffect
    {
        // Returns false (and applies nothing) if the effect cannot legally
        // be used right now, so the caller can surface feedback instead of
        // silently corrupting state.
        bool CanApply(GameSession session, PlayerData caster, PlayerData target);

        void Apply(GameSession session, PlayerData caster, PlayerData target);
    }
}