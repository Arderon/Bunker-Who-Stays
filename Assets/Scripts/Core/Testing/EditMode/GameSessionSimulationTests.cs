using Bunker.Core;
using Bunker.Core.Testing;
using NUnit.Framework;

public class GameSessionSimulationTests
{
    [Test]
    public void Simulation_AlwaysEndsWithExactlySurvivorsTarget_WhenNoAllPlayersEliminatedCase()
    {
        var runner = new HeadlessGameRunner(decisionSeed: 123);
        var result = runner.RunFullSimulation(
            fakePlayerCount: 10,
            traitPools: TestContentFactory.CreateDefaultTraitPools(),
            specialCardPool: TestContentFactory.CreateDefaultSpecialCardPool(),
            survivorsTarget: 4,
            dealSeed: 123);

        Assert.IsNotNull(result, "Simulation should complete without aborting.");
        Assert.AreEqual(GameOverResult.Reason.SurvivorsTargetReached, result.EndReason);
        Assert.AreEqual(4, result.Survivors.Count);
    }

    [Test]
    public void Simulation_NeverExceedsMaxIterations_AcrossManySeeds()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var runner = new HeadlessGameRunner(decisionSeed: seed);
            var result = runner.RunFullSimulation(
                fakePlayerCount: 6,
                traitPools: TestContentFactory.CreateDefaultTraitPools(),
                specialCardPool: TestContentFactory.CreateDefaultSpecialCardPool(),
                survivorsTarget: 2,
                dealSeed: seed);

            Assert.IsNotNull(result, $"Seed {seed} aborted or failed validation.");
        }
    }
}