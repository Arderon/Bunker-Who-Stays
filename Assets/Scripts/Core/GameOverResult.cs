using System.Collections.Generic;

namespace Bunker.Core
{
    // Describes why and how the game ended, so UI can show an appropriate
    // message instead of just "you won" for every case.
    public class GameOverResult
    {
        public enum Reason
        {
            SurvivorsTargetReached,   // normal win: exactly SurvivorsTarget players remain
            AllPlayersEliminated      // edge case: ties/rules led to zero survivors
        }

        public Reason EndReason;
        public List<PlayerData> Survivors;
    }
}