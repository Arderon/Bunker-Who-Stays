import { Schema, type } from "@colyseus/schema";

// The whole special card is private — only the owning client's StateView
// will ever contain this instance (see PlayerSchema.special, where the
// field itself is @view()-gated at the parent level rather than per-field
// here). Equivalent to the C# SpecialCardDto, which was only populated in
// BuildPlayerSnapshot when isOwner was true.
export class SpecialCardSchema extends Schema {
  @type("string") id: string = "";
  @type("string") effectType: string = "";
  @type("string") localizationKey: string = "";
}
