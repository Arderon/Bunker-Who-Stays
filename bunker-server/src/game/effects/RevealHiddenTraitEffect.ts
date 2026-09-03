import { ISpecialCardEffect } from "./ISpecialCardEffect";
import { GameSession } from "../GameSession";
import { PlayerData } from "../PlayerData";

// Lets the caster privately see one of the target's still-hidden traits,
// without revealing it to anyone else.
export class RevealHiddenTraitEffect implements ISpecialCardEffect {
  canApply(_session: GameSession, caster: PlayerData, target: PlayerData | null): boolean {
    if (!target || target.isEliminated) return false;
    if (target.playerId === caster.playerId) return false;
    return target.getTraits().some((t) => !target.isCategoryRevealed(t.category));
  }

  apply(_session: GameSession, _caster: PlayerData, _target: PlayerData | null): void {
    // Actual trait pick + private delivery is handled by GameSession
    // (needs to route the result only to the caster's client).
  }
}
