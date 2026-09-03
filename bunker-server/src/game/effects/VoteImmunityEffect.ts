import { ISpecialCardEffect } from "./ISpecialCardEffect";
import { GameSession } from "../GameSession";
import { PlayerData } from "../PlayerData";
import { GamePhase } from "../GamePhase";

// Grants the caster immunity from being eliminated by this round's vote.
// Self-targeted only, must be used before voting starts.
export class VoteImmunityEffect implements ISpecialCardEffect {
  canApply(session: GameSession, _caster: PlayerData, _target: PlayerData | null): boolean {
    return session.phase === GamePhase.Reveal || session.phase === GamePhase.Discussion;
  }

  apply(_session: GameSession, caster: PlayerData, _target: PlayerData | null): void {
    caster.hasVoteImmunityThisRound = true;
  }
}
