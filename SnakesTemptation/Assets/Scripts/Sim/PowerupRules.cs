using UnityEngine;

// rules for the lantern: when it shows up and how long the glow lasts.
// in the dark you can't see the seeds, while the lantern glows you can see them from anywhere
public class PowerupRules
{
    public float Duration = ChaseTuning.PowerupDuration;
    public float FirstDelay = ChaseTuning.PowerupFirstDelay;
    public float RespawnDelay = ChaseTuning.PowerupRespawnDelay;
    public int MinPath = ChaseTuning.PowerupMinPath;
    public int MaxPath = ChaseTuning.PowerupMaxPath;
    public int MinLead = ChaseTuning.PowerupMinLead;
    public int MaxDetour = ChaseTuning.PowerupMaxDetour;

    public float GlowLeft; // seconds of glow left, 0 = dark
    public int Collected;

    float spawnTimer;

    public bool Revealed
    {
        get { return GlowLeft > 0f; }
    }

    // the chase starts: no glow yet, the first lantern comes after FirstDelay
    public void Start()
    {
        GlowLeft = 0f;
        Collected = 0;
        spawnTimer = FirstDelay;
    }

    // picked up a lantern (picking up another one while glowing just resets the time)
    public void Collect()
    {
        GlowLeft = Duration;
        Collected++;
        spawnTimer = RespawnDelay;
    }

    // lost a life: the glow goes out and the lantern gets moved somewhere good for the new start
    public void OnRespawn(SnakeSim sim)
    {
        GlowLeft = 0f;
        if (sim.HasPowerup) sim.SpawnPowerup(MinPath, MaxPath, MinLead, MaxDetour);
    }

    public void Tick(float dt, SnakeSim sim)
    {
        if (GlowLeft > 0f) GlowLeft = Mathf.Max(0f, GlowLeft - dt);
        if (sim.HasPowerup) return;

        spawnTimer -= dt;
        if (spawnTimer > 0f) return;
        // no room right now, try again in a second
        if (!sim.SpawnPowerup(MinPath, MaxPath, MinLead, MaxDetour)) spawnTimer = 1f;
    }
}
