import { Schema, type } from "@colyseus/schema";

// Public-only trait slot: every client always sees that a slot exists and
// whether it's revealed. The actual content (id/localizationKey) is
// deliberately NOT stored here — @view()-gating those fields on a Schema
// instance living inside an ArraySchema was tested against a live server
// and found unreliable in this exact @colyseus/schema version (the field
// values never reached even the owning client, unlike an @view()-gated
// field on a direct, non-collection reference such as PlayerSchema.special,
// which worked correctly). Hidden/revealed trait content is instead
// delivered via explicit messages — "dealtHand" (private, to the owner,
// sent once at deal time) and "traitRevealed" (broadcast to everyone when
// a trait is revealed) — see BunkerRoom for both.
export class TraitSchema extends Schema {
  @type("uint8") category: number = 0;
  @type("boolean") revealed: boolean = false;
}