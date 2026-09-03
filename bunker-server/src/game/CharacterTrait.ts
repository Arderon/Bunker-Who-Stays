import { CardCategory } from "./CardCategory";

// Plain data — no methods, no framework dependency. Direct equivalent of
// the C# CharacterTrait POCO from section 1.1. Kept as an interface rather
// than a class since there's no behavior attached, just a value shape.
export interface CharacterTrait {
  id: string;
  category: CardCategory;
  localizationKey: string;
}

export function createTrait(id: string, category: CardCategory, localizationKey: string): CharacterTrait {
  return { id, category, localizationKey };
}
