import { CardCategory } from "../CardCategory";

// JSON-serializable content entry — direct equivalent of the C# TraitEntrySO,
// minus the Unity Editor/Inspector-specific parts (CreateAssetMenu, Tooltip).
export interface TraitEntry {
  id: string;
  category: CardCategory;
  localizationKey: string;
  weight: number; // default 1 if omitted in JSON — normalized on load, see loader below
  tags?: string[];
}
