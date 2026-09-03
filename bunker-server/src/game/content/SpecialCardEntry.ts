import { SpecialCardEffectTypeValue } from "../SpecialCard";

export interface SpecialCardEntry {
  id: string;
  effectType: SpecialCardEffectTypeValue;
  localizationKey: string;
}
