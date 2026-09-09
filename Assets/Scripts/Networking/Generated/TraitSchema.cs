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

public partial class TraitSchema : Schema {
#if UNITY_5_3_OR_NEWER
[Preserve]
#endif
public TraitSchema() { }
	[Type(0, "uint8")]
	public byte category = default(byte);

	[Type(1, "boolean")]
	public bool revealed = default(bool);
}

