// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 3.0.76
// 

using Colyseus.Schema;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Scripting;
#endif

public partial class GameStateSchema : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public GameStateSchema() { }
	[Type(0, "uint8")]
	public byte phase = default(byte);

	[Type(1, "uint16")]
	public ushort currentRound = default(ushort);

	[Type(2, "string")]
	public string currentTurnPlayerId = default(string);

	[Type(3, "map", typeof(MapSchema<PlayerSchema>))]
	public MapSchema<PlayerSchema> players = null;
}

