import { PlayerData } from "./PlayerData";

// Equivalent of the C# GameOverResult (section 1.7).
export enum GameOverReason {
  SurvivorsTargetReached = 0,
  AllPlayersEliminated = 1,
}

export interface GameOverResult {
  endReason: GameOverReason;
  survivors: PlayerData[];
}
