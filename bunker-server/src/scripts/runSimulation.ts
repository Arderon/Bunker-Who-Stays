import { HeadlessGameRunner } from "../game/testing/HeadlessGameRunner";
import { createDefaultTraitPools, createDefaultSpecialCardPool } from "../game/testing/TestContentFactory";

// Manual entry point, equivalent to the Unity ContextMenu debug button.
// Run with: npm run simulate
const runner = new HeadlessGameRunner(42);

const result = runner.runFullSimulation(
  8, // fakePlayerCount
  createDefaultTraitPools(),
  createDefaultSpecialCardPool(),
  3, // survivorsTarget
  42 // dealSeed
);

for (const line of runner.log) {
  console.log(line);
}

if (result) {
  console.log(`\nSimulation finished: ${result.endReason}, ${result.survivors.length} survivors.`);
} else {
  console.log("\nSimulation failed to start or complete.");
}
