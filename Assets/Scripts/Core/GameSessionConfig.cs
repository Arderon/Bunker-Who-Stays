using System;

namespace Bunker.Core
{
    // Immutable settings for a single game session, decided before the game starts
    // (e.g. from lobby UI). Kept separate from GameSession itself so it's easy
    // to serialize/log/replay a specific configuration.
    [Serializable]
    public class GameSessionConfig
    {
        public int SurvivorsTarget;          // how many players must remain to win
        public int? RandomSeed;              // optional, for deterministic testing/replay
        public bool AllowVoteTies = false;    // if false, ties trigger a re-vote

        public GameSessionConfig(int survivorsTarget, int? randomSeed = null)
        {
            SurvivorsTarget = survivorsTarget;
            RandomSeed = randomSeed;
        }
    }
}