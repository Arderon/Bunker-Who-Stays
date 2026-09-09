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

public partial class SpecialCardSchema : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public SpecialCardSchema() { }
	[Type(0, "string")]
	public string id = default(string);

	[Type(1, "string")]
	public string effectType = default(string);

	[Type(2, "string")]
	public string localizationKey = default(string);
}

