// Character trait categories. Numeric enum mirrors the C# CardCategory
// exactly (same order) so category indices stay consistent between the
// Unity client's local/offline mode and this server — useful if you ever
// need to cross-reference trait dumps or debug logs between the two.
export enum CardCategory {
  Gender = 0,
  Age = 1,
  Health = 2,
  Profession = 3,
  Baggage = 4,
  Hobby = 5,
  Fact = 6,
}

// All category values, in enum order — used wherever we need to iterate
// every category (dealing traits, building UI trait slots, etc.), since
// TypeScript enums don't expose a values() helper like C#'s Enum.GetValues.
export const ALL_CARD_CATEGORIES: CardCategory[] = [
  CardCategory.Gender,
  CardCategory.Age,
  CardCategory.Health,
  CardCategory.Profession,
  CardCategory.Baggage,
  CardCategory.Hobby,
  CardCategory.Fact,
];
