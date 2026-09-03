import { CardCategory } from "../CardCategory";
import { CharacterTrait, createTrait } from "../CharacterTrait";
import { createSpecialCard, SpecialCard } from "../SpecialCard";
import { PlayerData } from "../PlayerData";
import { TraitPool } from "./TraitPool";
import { TraitEntry } from "./TraitEntry";
import { SpecialCardPool } from "./SpecialCardPool";
import { SeededRandom } from "./SeededRandom";

// Direct port of the C# CharacterCardGenerator (section 1.2), including the
// no-repeat-within-game dealing rule, the allowRepeatsWithinGame override,
// weighted random selection, and the pool-exhaustion fallback.
export class CharacterCardGenerator {
  private readonly traitPools: TraitPool[];
  private readonly specialCardPool: SpecialCardPool;
  private readonly random: SeededRandom;

  constructor(traitPools: TraitPool[], specialCardPool: SpecialCardPool, seed?: number) {
    this.traitPools = traitPools;
    this.specialCardPool = specialCardPool;
    this.random = new SeededRandom(seed);
  }

  // Equivalent of the C# HasEnoughContentFor (section 1.7's addition to this class).
  hasEnoughContentFor(playerCount: number): boolean {
    for (const pool of this.traitPools) {
      if (!pool.allowRepeatsWithinGame && pool.entries.length < playerCount) {
        return false;
      }
    }
    return this.specialCardPool.entries.length > 0;
  }

  dealToPlayers(players: PlayerData[], withinGameNoRepeat = true): void {
    const usedIdsPerCategory = new Map<CardCategory, Set<string>>();
    for (const pool of this.traitPools) {
      usedIdsPerCategory.set(pool.category, new Set());
    }

    for (const player of players) {
      const traits: CharacterTrait[] = [];

      for (const pool of this.traitPools) {
        const usedIds = usedIdsPerCategory.get(pool.category)!;
        const enforceNoRepeat = withinGameNoRepeat && !pool.allowRepeatsWithinGame;

        let entry = this.pickEntry(pool, usedIds, enforceNoRepeat);

        if (!entry) {
          console.warn(`[CharacterCardGenerator] Pool '${pool.category}' exhausted, allowing repeat.`);
          entry = this.pickEntry(pool, usedIds, false);
        }

        if (!entry) {
          // Pool is genuinely empty — a content/config error, not a normal
          // exhaustion case. Fail loudly rather than silently deal a broken card.
          throw new Error(`[CharacterCardGenerator] Trait pool for category ${pool.category} has no entries.`);
        }

        usedIds.add(entry.id);
        traits.push(createTrait(entry.id, entry.category, entry.localizationKey));
      }

      player.assignTraits(traits);
      player.special = this.pickSpecialCard();
    }
  }

  private pickEntry(pool: TraitPool, usedIds: Set<string>, allowNoRepeat: boolean): TraitEntry | null {
    const candidates = allowNoRepeat
      ? pool.entries.filter((e) => !usedIds.has(e.id))
      : pool.entries;

    if (candidates.length === 0) return null;
    return this.weightedRandomPick(candidates);
  }

  private weightedRandomPick(candidates: TraitEntry[]): TraitEntry {
    const totalWeight = candidates.reduce((sum, e) => sum + Math.max(1, e.weight), 0);
    const roll = this.random.nextInt(totalWeight);

    let cumulative = 0;
    for (const entry of candidates) {
      cumulative += Math.max(1, entry.weight);
      if (roll < cumulative) return entry;
    }

    // Fallback, should not normally be reached — same safety net as the C# version.
    return candidates[candidates.length - 1];
  }

  private pickSpecialCard(): SpecialCard {
    const entries = this.specialCardPool.entries;
    const picked = entries[this.random.nextInt(entries.length)];
    return createSpecialCard(picked.id, picked.effectType, picked.localizationKey);
  }
}
