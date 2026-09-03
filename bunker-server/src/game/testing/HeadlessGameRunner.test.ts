import { HeadlessGameRunner } from "./HeadlessGameRunner";
import { createDefaultTraitPools, createDefaultSpecialCardPool } from "./TestContentFactory";
import { GameOverReason } from "../GameOverResult";

describe("GameSession simulation", () => {
  test("simulation always ends with exactly survivorsTarget when not all eliminated", () => {
    const runner = new HeadlessGameRunner(123);
    const result = runner.runFullSimulation(
      10,
      createDefaultTraitPools(),
      createDefaultSpecialCardPool(),
      4,
      123
    );

    expect(result).not.toBeNull();
    expect(result!.endReason).toBe(GameOverReason.SurvivorsTargetReached);
    expect(result!.survivors.length).toBe(4);
  });

  test("simulation never aborts across many seeds and player counts", () => {
    for (let players = 3; players <= 12; players++) {
      for (let seed = 0; seed < 20; seed++) {
        const runner = new HeadlessGameRunner(seed);
        const result = runner.runFullSimulation(
          players,
          createDefaultTraitPools(),
          createDefaultSpecialCardPool(),
          Math.max(1, Math.floor(players / 3)),
          seed
        );

        expect(result).not.toBeNull();
      }
    }
  });
});
