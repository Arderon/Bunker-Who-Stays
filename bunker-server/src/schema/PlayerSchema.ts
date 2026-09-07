import { Schema, type, view, ArraySchema } from "@colyseus/schema";
import { TraitSchema } from "./TraitSchema";
import { SpecialCardSchema } from "./SpecialCardSchema";

// Equivalent of the C# PlayerSnapshotDto. Public fields (playerId,
// displayName, isEliminated, hasUsedSpecialCard) are visible to everyone —
// there was never a reason to hide these in the C# version either.
//
// `special` is @view()-gated as a whole nested Schema: BunkerRoom adds a
// player's own SpecialCardSchema instance only to that player's own
// client.view, mirroring how the C# BuildPlayerSnapshot only attached
// SpecialCardDto when isOwner was true.
export class PlayerSchema extends Schema {
  @type("string") playerId: string = "";
  @type("string") displayName: string = "";
  @type("boolean") isEliminated: boolean = false;
  @type("boolean") hasUsedSpecialCard: boolean = false;

  @type([TraitSchema]) traits = new ArraySchema<TraitSchema>();

  @view() @type(SpecialCardSchema) special?: SpecialCardSchema;
}
