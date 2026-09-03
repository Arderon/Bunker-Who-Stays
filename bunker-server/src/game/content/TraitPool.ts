import { CardCategory } from "../CardCategory";
import { TraitEntry } from "./TraitEntry";

// Equivalent of the C# TraitPoolSO, including the allowRepeatsWithinGame
// flag we added to fix the Gender-pool exhaustion bug (see the earlier
// HasEnoughContentFor fix) — carried over here from the start rather than
// re-discovering the same issue on the server side.
export interface TraitPool {
  category: CardCategory;
  allowRepeatsWithinGame: boolean;
  entries: TraitEntry[];
}

export function isTraitPoolValid(pool: TraitPool): boolean {
  if (!pool.entries || pool.entries.length === 0) return false;
  return pool.entries.every((e) => e.category === pool.category && !!e.id);
}
