using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The lantern powerup's rules, with no GameObjects: when it appears, and how long the glow lasts. In the dark maze you
    /// cannot see the seeds; while the glow lasts you can, from anywhere. The controller and the balance tests share this.
    /// </summary>
    public sealed class PowerupRules
    {
        public float Duration = ChaseTuning.PowerupDuration;
        public float FirstDelay = ChaseTuning.PowerupFirstDelay;
        public float RespawnDelay = ChaseTuning.PowerupRespawnDelay;
        public int MinPath = ChaseTuning.PowerupMinPath;
        public int MaxPath = ChaseTuning.PowerupMaxPath;
        public int MinLead = ChaseTuning.PowerupMinLead;
        public int MaxDetour = ChaseTuning.PowerupMaxDetour;

        float spawnTimer;

        /// <summary>Seconds of glow left (0 = the maze is dark).</summary>
        public float GlowLeft { get; private set; }
        public bool Revealed => GlowLeft > 0f;
        public int Collected { get; private set; }

        /// <summary>Start of the chase: no glow, first lantern after <see cref="FirstDelay"/>.</summary>
        public void Start()
        {
            GlowLeft = 0f;
            Collected = 0;
            spawnTimer = FirstDelay;
        }

        /// <summary>The snake took a lantern: glow for the full duration (taking another while glowing refreshes it).</summary>
        public void Collect()
        {
            GlowLeft = Duration;
            Collected++;
            spawnTimer = RespawnDelay;
        }

        /// <summary>A life was lost: the glow goes out, and a lantern lying about is moved somewhere sensible for the new start.</summary>
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
            // if there is no room right now, try again in a second
            if (!sim.SpawnPowerup(MinPath, MaxPath, MinLead, MaxDetour)) spawnTimer = 1f;
        }
    }
}
