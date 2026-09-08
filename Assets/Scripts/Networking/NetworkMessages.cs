using System;
using System.Collections.Generic;

namespace Bunker.Networking
{
    // Plain message payload shapes matching BunkerRoom's send()/broadcast()
    // calls on the server. Consolidated here in one file — these were
    // previously declared separately in both ColyseusLobbyService.cs and
    // ColyseusGameSessionController.cs, which would have caused a duplicate
    // class definition compile error.

    [Serializable]
    public class ActionRejectedMessage
    {
        public string key;
    }

    // RevealHiddenTrait's private payload — sent only to the caster.
    [Serializable]
    public class PrivateTraitPeekMessage
    {
        public int category;
        public string id;
        public string localizationKey;
    }

    // One entry within a DealtHandMessage.
    [Serializable]
    public class TraitEntryMessage
    {
        public int category;
        public string id;
        public string localizationKey;
    }

    // Private: the local player's own full hand, sent once right after dealing.
    [Serializable]
    public class DealtHandMessage
    {
        public List<TraitEntryMessage> traits;
    }

    // Broadcast: any player's trait becoming publicly revealed.
    [Serializable]
    public class TraitRevealedMessage
    {
        public string playerId;
        public int category;
        public string id;
        public string localizationKey;
    }

    // Private: SwapTrait changed one of the local player's own hidden
    // traits without revealing it.
    [Serializable]
    public class TraitUpdatedMessage
    {
        public int category;
        public string id;
        public string localizationKey;
    }

    [Serializable]
    public class VotingResolvedMessage
    {
        public int resultType;
        public string eliminatedPlayerId;
        public List<string> tiedCandidateIds;
        public Dictionary<string, int> voteCounts;
    }

    [Serializable]
    public class GameOverMessage
    {
        public int endReason;
        public List<string> survivorPlayerIds;
    }
}
