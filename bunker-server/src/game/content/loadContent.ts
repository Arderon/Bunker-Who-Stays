import * as fs from "fs";
import * as path from "path";
import { TraitPool } from "./TraitPool";
import { SpecialCardPool } from "./SpecialCardPool";

// Loads all trait pool JSON files from a directory and the special card
// pool JSON file, normalizing optional fields (weight defaults to 1, same
// as TraitEntrySO's [Weight = 1] default in the C# version).
export function loadTraitPools(dirPath: string): TraitPool[] {
  const files = fs.readdirSync(dirPath).filter((f) => f.endsWith(".json"));

  return files.map((file) => {
    const raw = fs.readFileSync(path.join(dirPath, file), "utf-8");
    const pool = JSON.parse(raw) as TraitPool;

    pool.entries = pool.entries.map((e) => ({
      ...e,
      weight: e.weight ?? 1,
    }));

    return pool;
  });
}

export function loadSpecialCardPool(filePath: string): SpecialCardPool {
  const raw = fs.readFileSync(filePath, "utf-8");
  return JSON.parse(raw) as SpecialCardPool;
}
