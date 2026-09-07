import { PlayerData } from "./PlayerData";

// Equivalent of the C# VotingResult (introduced fully in section 1.6).
export enum VotingOutcome {
  PlayerEliminated = 0,
  TieRequiresRevote = 1,
  TieUnresolvedNoElimination = 2, // kept for allowVoteTies=true configurations; not reached by default anymore
  NoVotesCast = 3,
  TieResolvedRandomly = 4, // new: forced random elimination among tied candidates after max re-vote attempts
}

export interface VotingResult {
  resultType: VotingOutcome;
  eliminatedPlayer: PlayerData | null;
  tiedCandidates: PlayerData[] | null;
  voteCounts: Map<string, number>;
}
