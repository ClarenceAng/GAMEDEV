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
        Assert.Greater(startZ, 55f, "Lasers should spawn at the far end of the corridor");

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
        manager.Teleport(new Vector3(0, 0.1f, 59f), Quaternion.identity);

        yield return WaitUntilOrTimeout(() => manager.CurrentState == LaserCorridorManager.State.Complete, 1f);

        Assert.AreEqual(LaserCorridorManager.State.Complete, manager.CurrentState);
        Assert.IsFalse(spawner.IsRunning);
    }

    static FloorPanel FindPanel(BonusType bonus)
    {
        return Object.FindObjectsByType<FloorPanel>(FindObjectsSortMode.InstanceID).First(p => p.Bonus == bonus);
    }

    IEnumerator StepOn(FloorPanel panel)
    {
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
