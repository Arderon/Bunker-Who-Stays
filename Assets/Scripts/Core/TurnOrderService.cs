using System;
using System.Collections.Generic;
using System.Linq;

namespace Bunker.Core
{
    // Determines whose turn it is to reveal a trait during the Reveal phase.
    // Kept separate from GameSession so the turn-order rule (rotate starting
    // player each round) can be tested and tweaked in isolation.
    public class TurnOrderService
    {
        private List<string> _turnOrder = new(); // ordered list of PlayerIds
        private int _currentTurnIndex;

        // Builds the initial order once, at game start, and remembers it.
        // Active-only players are recalculated per round via RebuildForRound.
        public void Initialize(IEnumerable<PlayerData> players)
        {
            _turnOrder = players.Select(p => p.PlayerId).ToList();
            _currentTurnIndex = 0;
        }

        // Called at the start of every round. Rotates the starting player by one
        // position so the same player doesn't always go first, and drops
        // eliminated players from the rotation.
        public void RebuildForRound(int roundNumber, IEnumerable<PlayerData> activePlayers)
        {
            var activeIds = activePlayers.Select(p => p.PlayerId).ToHashSet();

            // Preserve relative order, but only keep still-active players.
            _turnOrder = _turnOrder.Where(id => activeIds.Contains(id)).ToList();

            if (_turnOrder.Count == 0) return;

            // Rotate start offset based on round number so turn 1 of round 2
            // starts with a different player than round 1 did.
            int offset = (roundNumber - 1) % _turnOrder.Count;
            _turnOrder = _turnOrder.Skip(offset).Concat(_turnOrder.Take(offset)).ToList();

            _currentTurnIndex = 0;
        }

        public string CurrentPlayerId => _turnOrder.Count > 0 ? _turnOrder[_currentTurnIndex] : null;

        public bool IsPlayersTurn(string playerId) => CurrentPlayerId == playerId;

        // Advances to the next player in the rotation. Returns false if the
        // round has looped back to the start (a full pass has been completed).
        public bool AdvanceTurn()
        {
            if (_turnOrder.Count == 0) return false;

            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            return _currentTurnIndex != 0;
        }
    }
}