// Equivalent of the C# GameSessionConfig (section 1.3). Plain data, no
// framework dependency — decided at lobby time, same as the C# version.
export interface GameSessionConfig {
  survivorsTarget: number;
  randomSeed?: number;
  allowVoteTies: boolean;
}

export function createGameSessionConfig(
  survivorsTarget: number,
  randomSeed?: number
): GameSessionConfig {
  return { survivorsTarget, randomSeed, allowVoteTies: false };
}
