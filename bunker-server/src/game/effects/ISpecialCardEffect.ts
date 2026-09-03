import { GameSession } from "../GameSession";
import { PlayerData } from "../PlayerData";

// Equivalent of the C# ISpecialCardEffect. Same split as the C# version:
// canApply is fully self-contained per effect, while apply() for effects
// needing extra data (which category to swap, etc.) is a no-op here —
// actual execution lives in GameSession, keyed by effectType (see the
// GameSession additions below). See the C# section 1.5 rationale for why
// this split was chosen over widening the interface signature.
export interface ISpecialCardEffect {
  canApply(session: GameSession, caster: PlayerData, target: PlayerData | null): boolean;
  apply(session: GameSession, caster: PlayerData, target: PlayerData | null): void;
}
