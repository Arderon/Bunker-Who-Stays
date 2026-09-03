// Node's Math.random() cannot be seeded, so deterministic dealing/testing
// (mirroring C#'s `new System.Random(seed)` from section 1.2/1.8) needs a
// small seedable PRNG. mulberry32 is a common, simple, well-tested choice
// for this — not cryptographically secure, but that's not a requirement here.
export class SeededRandom {
  private state: number;

  constructor(seed?: number) {
    // If no seed given, derive one from the current time so behavior stays
    // "random enough" for real games, same as `new System.Random()` with no seed.
    this.state = seed ?? Date.now();
  }

  // Returns a float in [0, 1), analogous to System.Random.NextDouble().
  nextFloat(): number {
    this.state |= 0;
    this.state = (this.state + 0x6d2b79f5) | 0;
    let t = Math.imul(this.state ^ (this.state >>> 15), 1 | this.state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  // Returns an integer in [0, maxExclusive), analogous to System.Random.Next(max).
  nextInt(maxExclusive: number): number {
    return Math.floor(this.nextFloat() * maxExclusive);
  }
}
