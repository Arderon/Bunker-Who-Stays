// String-based effect type, not a TS union/enum, mirroring the C# decision
// (section 1.1): keeps effect types data-driven, so new special cards can
// be added via content (JSON) without a code change, as long as a matching
// effect implementation exists in the effect registry (section 2.5).
export const SpecialCardEffectType = {
  RevealHiddenTrait: "RevealHiddenTrait",
  VoteImmunity: "VoteImmunity",
  SwapTrait: "SwapTrait",
  ForceRevealAll: "ForceRevealAll",
} as const;

// Union of the string literal values above (e.g. "RevealHiddenTrait" | "VoteImmunity" | ...).
// Using this instead of `string` for SpecialCard.effectType gives compile-time
// checking at call sites while still storing a plain string at runtime —
// same flexibility as the C# string-based approach, with a bit more safety.
export type SpecialCardEffectTypeValue =
  (typeof SpecialCardEffectType)[keyof typeof SpecialCardEffectType];

export interface SpecialCard {
  id: string;
  effectType: SpecialCardEffectTypeValue;
  localizationKey: string;
}

export function createSpecialCard(
  id: string,
  effectType: SpecialCardEffectTypeValue,
  localizationKey: string
): SpecialCard {
  return { id, effectType, localizationKey };
}
