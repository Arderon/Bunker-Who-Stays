using System.Collections.Generic;

namespace Bunker.Core
{
    // Describes the outcome of a vote tally. Returned by GameSession so the
    // caller (UI / network layer) can react precisely — show a tie screen,
    // announce an elimination, etc. — instead of inferring it from side effects.
    public class VotingResult
    {
        public enum Outcome
        {
            PlayerEliminated,
            TieRequiresRevote,
            TieUnresolvedNoElimination, // tie persisted after max re-vote attempts
            NoVotesCast
        }

        public Outcome ResultType;
        public PlayerData EliminatedPlayer;          // set only when ResultType == PlayerEliminated
        public List<PlayerData> TiedCandidates;       // set only for tie outcomes
        public Dictionary<string, int> VoteCounts;    // playerId -> vote count, for UI display
    }
}