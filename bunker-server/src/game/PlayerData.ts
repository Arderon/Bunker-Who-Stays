import { CardCategory } from "./CardCategory";
import { CharacterTrait } from "./CharacterTrait";
import { SpecialCard } from "./SpecialCard";

// Direct port of the C# PlayerData class (section 1.1), kept as a class
// with methods (rather than plain data + free functions) specifically to
// preserve a 1:1 mental mapping with the C# version — makes future logic
// changes easier to cross-check between the two implementations.
export class PlayerData {
  public readonly playerId: string;
  public displayName: string;

  private traits: CharacterTrait[] = [];
  public special: SpecialCard | null = null;

  // JS has no built-in Set<T> equality-by-value issue here since CardCategory
  // is a number enum — Set<CardCategory> behaves the same as C#'s
  // HashSet<CardCategory>, including O(1) .has() lookups.
  private revealedCategories: Set<CardCategory> = new Set();

  public isEliminated = false;
  public hasUsedSpecialCard = false;
  public hasVoteImmunityThisRound = false;

  constructor(playerId: string, displayName: string) {
    this.playerId = playerId;
    this.displayName = displayName;
  }

  assignTraits(traits: CharacterTrait[]): void {
    this.traits = [...traits];
  }

  getTraits(): readonly CharacterTrait[] {
    return this.traits;
  }

  getTrait(category: CardCategory): CharacterTrait | undefined {
    return this.traits.find((t) => t.category === category);
  }

  // Equivalent to C#'s ReplaceTrait, used by the SwapTrait special card effect (section 2.5).
  replaceTrait(category: CardCategory, newTrait: CharacterTrait): void {
    const index = this.traits.findIndex((t) => t.category === category);
    if (index >= 0) {
      this.traits[index] = newTrait;
    }
  }

  isCategoryRevealed(category: CardCategory): boolean {
    return this.revealedCategories.has(category);
  }

  revealCategory(category: CardCategory): void {
    this.revealedCategories.add(category);
  }

  getRevealedCategories(): ReadonlySet<CardCategory> {
    return this.revealedCategories;
  }

  hasUnrevealedTraits(): boolean {
    return this.revealedCategories.size < this.traits.length;
  }

  resetPerRoundFlags(): void {
    this.hasVoteImmunityThisRound = false;
  }
}
