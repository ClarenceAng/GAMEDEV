using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// The revolver's six-shot cylinder and its timing, kept free of Unity objects so it can be unit tested.
    /// A reload always refills all six chambers; it starts by itself when the last round is fired.
    /// </summary>
    public sealed class Chamber
    {
        public const int Capacity = 6;

        public float ReloadTime { get; }
        public float FireInterval { get; }

        public int Rounds { get; private set; } = Capacity;
        public bool Reloading { get; private set; }

        /// <summary>0..1 progress of the current reload (0 when not reloading).</summary>
        public float ReloadProgress => Reloading ? 1f - reloadLeft / ReloadTime : 0f;

        float reloadLeft, cooldown;

        public Chamber(float reloadTime = 1.1f, float fireInterval = 0.16f)
        {
            ReloadTime = reloadTime;
            FireInterval = fireInterval;
        }

        public bool CanFire => !Reloading && Rounds > 0 && cooldown <= 0f;

        /// <summary>Uses one round if the gun is ready. Emptying the cylinder starts the reload.</summary>
        public bool TryFire()
        {
            if (!CanFire) return false;
            Rounds--;
            cooldown = FireInterval;
            if (Rounds == 0) StartReload();
            return true;
        }

        /// <summary>Starts a reload unless one is running or the cylinder is already full. Returns true if it started.</summary>
        public bool StartReload()
        {
            if (Reloading || Rounds == Capacity) return false;
            Reloading = true;
            reloadLeft = ReloadTime;
            return true;
        }

        /// <summary>Advances the timers. Returns true on the tick a reload finishes.</summary>
        public bool Tick(float dt)
        {
            cooldown = Mathf.Max(0f, cooldown - dt);
            if (!Reloading) return false;
            reloadLeft -= dt;
            if (reloadLeft > 0f) return false;
            Reloading = false;
            Rounds = Capacity;
            return true;
        }
    }
}
