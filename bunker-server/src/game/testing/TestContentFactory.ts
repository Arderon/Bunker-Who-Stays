import { CardCategory } from "../CardCategory";
import { TraitPool } from "../content/TraitPool";
import { SpecialCardPool } from "../content/SpecialCardPool";
import { SpecialCardEffectType } from "../SpecialCard";

// In-memory test content, mirroring the Unity TestContentFactory used for
// EditMode tests. Avoids depending on the real JSON content files loaded
// via loadTraitPools/loadSpecialCardPool (section 2.2), so these tests
// don't break if the content files are edited or incomplete.
export function createDefaultTraitPools(): TraitPool[] {
  const categories = [
    CardCategory.Gender,
    CardCategory.Age,
    CardCategory.Health,
    CardCategory.Profession,
    CardCategory.Baggage,
    CardCategory.Hobby,
    CardCategory.Fact,
  ];

  return categories.map((category) => ({
    category,
    // Same rule as the real content: Gender/Age/Health are naturally
    // shared between players, the rest default to unique-per-game.
    allowRepeatsWithinGame:
      category === CardCategory.Gender || category === CardCategory.Age || category === CardCategory.Health,
    entries: Array.from({ length: 10 }, (_, i) => ({
      id: `${CardCategory[category]}_${i}`,
      category,
      localizationKey: `trait_${CardCategory[category]}_${i}`,
      weight: 1,
    })),
  }));
}

export function createDefaultSpecialCardPool(): SpecialCardPool {
  const effectTypes = [
    SpecialCardEffectType.RevealHiddenTrait,
    SpecialCardEffectType.VoteImmunity,
    SpecialCardEffectType.SwapTrait,
    SpecialCardEffectType.ForceRevealAll,
  ];

  return {
    entries: effectTypes.map((effect) => ({
      id: `special_${effect}`,
      effectType: effect,
      localizationKey: `special_${effect}`,
    })),
  };
}
