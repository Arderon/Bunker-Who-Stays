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

public partial class PlayerSchema : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public PlayerSchema() { }
	[Type(0, "string")]
	public string playerId = default(string);

	[Type(1, "string")]
	public string displayName = default(string);

	[Type(2, "boolean")]
	public bool isEliminated = default(bool);

	[Type(3, "boolean")]
	public bool hasUsedSpecialCard = default(bool);

	[Type(4, "array", typeof(ArraySchema<TraitSchema>))]
	public ArraySchema<TraitSchema> traits = null;

	[Type(5, "ref", typeof(SpecialCardSchema))]
	public SpecialCardSchema special = null;
}

