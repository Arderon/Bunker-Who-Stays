import { PlayerData } from "./PlayerData";

// Equivalent of the C# VotingResult (introduced fully in section 1.6).
export enum VotingOutcome {
  PlayerEliminated = 0,
  TieRequiresRevote = 1,
  TieUnresolvedNoElimination = 2,
  NoVotesCast = 3,
}

export interface VotingResult {
  resultType: VotingOutcome;
  eliminatedPlayer: PlayerData | null;
  tiedCandidates: PlayerData[] | null;
  voteCounts: Map<string, number>;
}
