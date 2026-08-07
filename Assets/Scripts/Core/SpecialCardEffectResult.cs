namespace Bunker.Core
{
    // Describes what actually happened after an effect was applied.
    // Sent back to the caller (and, later, over the network) so the UI knows
    // what to show — e.g. a revealed trait should only be shown to the caster,
    // not broadcast to everyone.
    public class SpecialCardEffectResult
    {
        public bool Success;
        public string FailReason;

        // Set only for effects that reveal private info to the caster
        // (e.g. RevealHiddenTrait). Null for effects with no private payload.
        public CharacterTrait RevealedTrait;

        public static SpecialCardEffectResult Fail(string reason) =>
            new() { Success = false, FailReason = reason };

        public static SpecialCardEffectResult Ok(CharacterTrait revealedTrait = null) =>
            new() { Success = true, RevealedTrait = revealedTrait };
    }
}