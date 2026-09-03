// Direct port of the C# GamePhase enum (section 1.3), extended with
// VotingTiebreaker from section 1.6 — included from the start here since
// we already know it's needed, unlike the C# version where it was added later.
export enum GamePhase {
  Lobby = 0,
  Dealing = 1,
  Reveal = 2,
  Discussion = 3,
  Voting = 4,
  VotingTiebreaker = 5,
  RoundResult = 6,
  GameOver = 7,
}
