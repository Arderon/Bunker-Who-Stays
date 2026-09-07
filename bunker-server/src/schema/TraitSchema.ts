import { Schema, type, view } from "@colyseus/schema";

// Replaces the manually-written TraitDto + the hand-rolled filtering logic
// from BuildPlayerSnapshot in the C#/Netcode version. There, we manually
// decided per-recipient whether to include a trait's id/localizationKey in
// the outgoing DTO. Here, the same effect is achieved declaratively: fields
// marked @view() are only sent to clients whose StateView explicitly
// includes this specific TraitSchema instance (see BunkerRoom for where
// that inclusion actually happens).
//
// category and revealed stay public — every client always knows a trait
// slot exists and whether it's been revealed, same as the C# version
// always sending a slot placeholder. Only the actual content (id,
// localizationKey) is view-gated.
export class TraitSchema extends Schema {
  @type("uint8") category: number = 0;
  @type("boolean") revealed: boolean = false;

  @view() @type("string") id: string = "";
  @view() @type("string") localizationKey: string = "";
}
