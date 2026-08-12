using Bunker.Core.Testing;
using System.Collections.Generic;
using UnityEngine;

public class HeadlessRunnerDebugEntry : MonoBehaviour
{
    [SerializeField] private List<Bunker.Content.TraitPoolSO> _traitPools;
    [SerializeField] private Bunker.Content.SpecialCardPoolSO _specialCardPool;

    [ContextMenu("Run Simulation")]
    private void RunSimulation()
    {
        var runner = new HeadlessGameRunner(decisionSeed: 42);
        var result = runner.RunFullSimulation(
            fakePlayerCount: 8,
            traitPools: _traitPools,
            specialCardPool: _specialCardPool,
            survivorsTarget: 3,
            dealSeed: 42);

        foreach (var line in runner.Log)
        {
            Debug.Log(line);
        }

        if (result != null)
        {
            Debug.Log($"Simulation finished: {result.EndReason}, " +
                       $"{result.Survivors.Count} survivors.");
        }
    }
}