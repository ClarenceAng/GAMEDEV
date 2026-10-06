using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Plays the generated LaserCorridor scene and checks each assignment requirement:
// lasers come from the far end and hurt, panels give bonuses, death resets, the exit wins.
public class LaserCorridorTests
{
    LaserCorridorManager manager;
    PlayerHealth player;
    LaserSpawner spawner;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("LaserCorridor", LoadSceneMode.Single);
        yield return null;
        yield return null;

        manager = LaserCorridorManager.Instance;
        Assert.IsNotNull(manager, "LaserCorridorManager missing from scene");
        player = manager.Player;
        spawner = manager.Spawner;
    }

    [UnityTest]
    public IEnumerator Lasers_SpawnAtFarEnd_AndTravelTowardsStart()
    {
        LaserWave wave = spawner.Spawn(LaserPattern.LowSweep);
        float startZ = wave.transform.position.z;
        Assert.Greater(startZ, GoalZ(), "Lasers should spawn at the far end of the corridor, beyond the goal");

        yield return new WaitForSeconds(0.5f);

        Assert.Less(wave.transform.position.z, startZ, "Lasers should move towards the player/start");
    }

    [UnityTest]
    public IEnumerator Laser_TouchingPlayer_ReducesHealth()
    {
        spawner.Stop();
        spawner.ClearAll();
        spawner.Spawn(LaserPattern.LowSweep, player.transform.position.z + 2f);

        yield return WaitUntilOrTimeout(() => player.Current < player.Max, 3f);

        Assert.Less(player.Current, player.Max);
    }

    [UnityTest]
    public IEnumerator ShieldBonus_BlocksLaserDamage()
    {
        spawner.Stop();
        spawner.ClearAll();
        Assert.IsTrue(manager.ApplyBonus(BonusType.Shield));
        LaserWave wave = spawner.Spawn(LaserPattern.LowSweep, player.transform.position.z + 2f);

        yield return WaitUntilOrTimeout(() => wave == null || wave.transform.position.z < player.transform.position.z - 1f, 3f);

        Assert.AreEqual(player.Max, player.Current);
    }

    [UnityTest]
    public IEnumerator MedkitPanel_HealsPlayer_AndGoesDark()
    {
        player.TakeDamage(50);
        FloorPanel panel = FindPanel(BonusType.Medkit);

        yield return StepOn(panel);

        Assert.AreEqual(player.Max - 50 + 40, player.Current);
        Assert.IsTrue(panel.IsUsed);
    }

    [UnityTest]
    public IEnumerator TimeSlowPanel_SlowsLasers()
    {
        yield return StepOn(FindPanel(BonusType.TimeSlow));

        Assert.Less(spawner.SpeedMultiplier, 1f);
    }

    [UnityTest]
    public IEnumerator SpeedPanel_BoostsPlayer()
    {
        yield return StepOn(FindPanel(BonusType.SpeedBoost));

        Assert.IsTrue(manager.IsSpeedBoosted);
    }

    [UnityTest]
    public IEnumerator LosingAllHealth_EndsRun_AndResetsToStart()
    {
        FloorPanel panel = FindPanel(BonusType.Medkit);
        yield return StepOn(panel);
        // A stray hit would start the invulnerability window and block the killing blow.
        spawner.Stop();
        spawner.ClearAll();
        Vector3 startPosition = new Vector3(0, 0, -2f);
        manager.Teleport(new Vector3(0, 0.1f, 30f), Quaternion.identity);

        player.TakeDamage(1000);

        Assert.AreEqual(LaserCorridorManager.State.Dead, manager.CurrentState);
        Assert.AreEqual(1, manager.Deaths);

        yield return new WaitForSeconds(3f);

        Assert.AreEqual(LaserCorridorManager.State.Playing, manager.CurrentState);
        Assert.AreEqual(player.Max, player.Current);
        Assert.Less(Vector3.Distance(player.transform.position, startPosition), 1f, "Player should be back at the start");
        Assert.IsFalse(panel.IsUsed, "Panels should be re-armed after a reset");
    }

    [UnityTest]
    public IEnumerator ReachingTheEnd_CompletesLevel()
    {
        manager.Teleport(new Vector3(0, 0.1f, GoalZ()), Quaternion.identity);

        yield return WaitUntilOrTimeout(() => manager.CurrentState == LaserCorridorManager.State.Complete, 1f);

        Assert.AreEqual(LaserCorridorManager.State.Complete, manager.CurrentState);
        Assert.IsFalse(spawner.IsRunning);
    }

    [UnityTest]
    public IEnumerator EveryPattern_SpawnsAndMoves()
    {
        spawner.Stop();
        spawner.ClearAll();
        foreach (LaserPattern pattern in System.Enum.GetValues(typeof(LaserPattern)))
        {
            LaserWave wave = spawner.Spawn(pattern, 80f);
            Assert.Greater(wave.GetComponentsInChildren<LaserBeam>().Length, 0, pattern + " has no beams");
        }

        yield return new WaitForSeconds(0.3f);

        LogAssert.NoUnexpectedReceived();
        Assert.IsTrue(spawner.ActiveWaves.All(w => w.transform.position.z < 80f));
    }

    [Test]
    public void MostWaves_AreComplexMovingPatterns_AndNeverRepeatBackToBack()
    {
        var complex = new[] { LaserPattern.LowAndSweeper, LaserPattern.Scissor, LaserPattern.Spinner, LaserPattern.MovingGap };
        const int samples = 4000;
        int complexCount = 0;
        LaserPattern previous = spawner.PickPattern();
        for (int i = 0; i < samples; i++)
        {
            LaserPattern pattern = spawner.PickPattern();
            Assert.AreNotEqual(previous, pattern, "Same pattern twice in a row");
            if (complex.Contains(pattern))
            {
                complexCount++;
            }
            previous = pattern;
        }

        float share = (float)complexCount / samples;
        Assert.That(share, Is.InRange(0.77f, 0.83f), $"Complex share was {share:P0}");
    }

    [UnityTest]
    public IEnumerator Spinner_IsSafeWhenHuggingAWall()
    {
        spawner.Stop();
        spawner.ClearAll();
        manager.Teleport(new Vector3(2.5f, 0.1f, 5f), Quaternion.identity);
        yield return new WaitForFixedUpdate();
        LaserWave wave = spawner.Spawn(LaserPattern.Spinner, player.transform.position.z + 3f);

        yield return WaitUntilOrTimeout(() => wave == null || wave.transform.position.z < player.transform.position.z - 1f, 3f);

        Assert.AreEqual(player.Max, player.Current);
    }

    [UnityTest]
    public IEnumerator Spinner_HitsPlayerInTheMiddle()
    {
        spawner.Stop();
        spawner.ClearAll();
        manager.Teleport(new Vector3(0f, 0.1f, 5f), Quaternion.identity);
        yield return new WaitForFixedUpdate();
        spawner.Spawn(LaserPattern.Spinner, player.transform.position.z + 3f);

        yield return WaitUntilOrTimeout(() => player.Current < player.Max, 3f);

        Assert.Less(player.Current, player.Max);
    }

    [UnityTest]
    public IEnumerator Run_StartsWithWavesAlreadyInFlight_ButClearNearStart()
    {
        Assert.Greater(spawner.ActiveWaves.Count, 3);
        Assert.IsTrue(spawner.ActiveWaves.All(w => w.transform.position.z > 10f));
        yield break;
    }

    static float GoalZ()
    {
        return Object.FindFirstObjectByType<CorridorGoal>().transform.position.z;
    }

    static FloorPanel FindPanel(BonusType bonus)
    {
        return Object.FindObjectsByType<FloorPanel>(FindObjectsSortMode.InstanceID).First(p => p.Bonus == bonus);
    }

    // Clears the lasers first so a stray hit can't skew the health checks.
    IEnumerator StepOn(FloorPanel panel)
    {
        spawner.Stop();
        spawner.ClearAll();
        manager.Teleport(panel.transform.position + Vector3.up * 0.1f, Quaternion.identity);
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        yield return null;
    }

    static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
    {
        float end = Time.time + timeout;
        while (!condition() && Time.time < end)
        {
            yield return null;
        }
    }
}
