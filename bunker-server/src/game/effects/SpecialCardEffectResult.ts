import { CharacterTrait } from "../CharacterTrait";

// Equivalent of the C# SpecialCardEffectResult (section 1.5).
export interface SpecialCardEffectResult {
  success: boolean;
  failReason?: string;
  revealedTrait?: CharacterTrait; // set only for effects with a private payload (RevealHiddenTrait)
}

export function fail(reason: string): SpecialCardEffectResult {
  return { success: false, failReason: reason };
}

export function ok(revealedTrait?: CharacterTrait): SpecialCardEffectResult {
  return { success: true, revealedTrait };
}
