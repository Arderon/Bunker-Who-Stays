import { ISpecialCardEffect } from "./ISpecialCardEffect";
import { GameSession } from "../GameSession";
import { PlayerData } from "../PlayerData";

// Forces the target to reveal one random still-hidden trait to everyone.
export class ForceRevealAllEffect implements ISpecialCardEffect {
  canApply(_session: GameSession, caster: PlayerData, target: PlayerData | null): boolean {
    if (!target || target.isEliminated) return false;
    if (target.playerId === caster.playerId) return false;
    return target.hasUnrevealedTraits();
  }

  apply(_session: GameSession, _caster: PlayerData, _target: PlayerData | null): void {
    // No-op here — picking which hidden trait gets forcibly revealed and
    // firing the public traitRevealed event is handled by GameSession,
    // which already owns that event.
  }
}
