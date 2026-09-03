import { PlayerData } from "./PlayerData";

// Direct port of the C# TurnOrderService (section 1.4). Determines whose
// turn it is during the Reveal phase, and rotates the starting player each
// round using the same offset-based rule as the C# version (not fully
// random), for the same fairness reasoning discussed there.
export class TurnOrderService {
  private turnOrder: string[] = []; // ordered PlayerIds
  private currentTurnIndex = 0;

  initialize(players: readonly PlayerData[]): void {
    this.turnOrder = players.map((p) => p.playerId);
    this.currentTurnIndex = 0;
  }

  // Called at the start of every round. Rotates the starting player by one
  // position (based on roundNumber) and drops eliminated players from the
  // rotation, same as the C# RebuildForRound.
  rebuildForRound(roundNumber: number, activePlayers: readonly PlayerData[]): void {
    const activeIds = new Set(activePlayers.map((p) => p.playerId));

    // Preserve relative order, keep only still-active players.
    this.turnOrder = this.turnOrder.filter((id) => activeIds.has(id));

    if (this.turnOrder.length === 0) return;

    const offset = (roundNumber - 1) % this.turnOrder.length;
    this.turnOrder = [...this.turnOrder.slice(offset), ...this.turnOrder.slice(0, offset)];

    this.currentTurnIndex = 0;
  }

  get currentPlayerId(): string | null {
    return this.turnOrder.length > 0 ? this.turnOrder[this.currentTurnIndex] : null;
  }

  isPlayersTurn(playerId: string): boolean {
    return this.currentPlayerId === playerId;
  }

  // Advances to the next player in rotation. Returns false once the round
  // has looped back to the start (a full pass through all active players
  // is complete), same as the C# AdvanceTurn.
  advanceTurn(): boolean {
    if (this.turnOrder.length === 0) return false;

    this.currentTurnIndex = (this.currentTurnIndex + 1) % this.turnOrder.length;
    return this.currentTurnIndex !== 0;
  }
}
