import { ISpecialCardEffect } from "./ISpecialCardEffect";
import { GameSession } from "../GameSession";
import { PlayerData } from "../PlayerData";

// Swaps one still-hidden trait category between the caster and the target.
// Category selection happens in GameSession (extra parameter needed) —
// this canApply only checks that at least one swappable category exists.
export class SwapTraitEffect implements ISpecialCardEffect {
  canApply(_session: GameSession, caster: PlayerData, target: PlayerData | null): boolean {
    if (!target || target.isEliminated) return false;
    if (target.playerId === caster.playerId) return false;

    return caster
      .getTraits()
      .some((t) => !caster.isCategoryRevealed(t.category) && !target.isCategoryRevealed(t.category));
  }

  apply(_session: GameSession, _caster: PlayerData, _target: PlayerData | null): void {
    // No-op here — actual swap logic lives in GameSession.useSwapTraitSpecialCard,
    // which has the extra "which category" parameter this interface doesn't carry.
  }
}
