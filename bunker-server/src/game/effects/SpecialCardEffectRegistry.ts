import { SpecialCardEffectType, SpecialCardEffectTypeValue } from "../SpecialCard";
import { ISpecialCardEffect } from "./ISpecialCardEffect";
import { RevealHiddenTraitEffect } from "./RevealHiddenTraitEffect";
import { VoteImmunityEffect } from "./VoteImmunityEffect";
import { SwapTraitEffect } from "./SwapTraitEffect";
import { ForceRevealAllEffect } from "./ForceRevealAllEffect";

// Maps a SpecialCard's effectType string to its concrete implementation.
// Same rationale as the C# registry: string-keyed rather than enum-keyed,
// so new cards can be added via content as long as a matching effect class
// is registered here.
export class SpecialCardEffectRegistry {
  private readonly effects = new Map<SpecialCardEffectTypeValue, ISpecialCardEffect>();

  constructor() {
    this.register(SpecialCardEffectType.RevealHiddenTrait, new RevealHiddenTraitEffect());
    this.register(SpecialCardEffectType.VoteImmunity, new VoteImmunityEffect());
    this.register(SpecialCardEffectType.SwapTrait, new SwapTraitEffect());
    this.register(SpecialCardEffectType.ForceRevealAll, new ForceRevealAllEffect());
  }

  private register(effectType: SpecialCardEffectTypeValue, effect: ISpecialCardEffect): void {
    this.effects.set(effectType, effect);
  }

  tryGet(effectType: SpecialCardEffectTypeValue): ISpecialCardEffect | undefined {
    return this.effects.get(effectType);
  }
}
