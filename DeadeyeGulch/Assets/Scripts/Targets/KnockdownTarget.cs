using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// Anything that can be shot down for points. Subclasses decide what a hit does (shatter, tumble, flip over) and call
    /// <see cref="KnockDown"/> once the target counts as down; the game pays for it in <see cref="KnockdownGame.OnTargetDown"/>.
    /// </summary>
    public abstract class KnockdownTarget : MonoBehaviour
    {
        [Tooltip("Base points when it goes down (before the streak multiplier and long-range bonus).")]
        public int points = 50;

        [Tooltip("An innocent townsperson: shooting it costs points instead of earning them.")]
        public bool innocent;

        [Tooltip("Shown in the score popup, e.g. TRICK SHOT. Empty for none.")]
        public string bonusLabel = "";

        public KnockdownGame Game { get; set; }

        public bool IsDown { get; private set; }

        /// <summary>False while it can't be scored: down, hidden, or (for a tossed can) already landed.</summary>
        public virtual bool IsLive => !IsDown;

        /// <summary>The point a marksman would aim at.</summary>
        public virtual Vector3 AimPoint => transform.position;

        /// <summary>How fast the aim point is moving (world units per second). Lets tests lead a moving target.</summary>
        public virtual Vector3 Velocity => measuredVelocity;

        Vector3 lastAim, measuredVelocity;
        bool hasLast;

        protected virtual void Update() { }

        // Measured after every Update, so it sees this frame's movement whichever script moved the target.
        void LateUpdate()
        {
            Vector3 aim = AimPoint;
            if (hasLast && Time.deltaTime > 0f) measuredVelocity = (aim - lastAim) / Time.deltaTime;
            lastAim = aim;
            hasLast = true;
        }

        /// <summary>A bullet struck this target at <paramref name="point"/> travelling along <paramref name="dir"/>.</summary>
        public abstract void TakeHit(Vector3 point, Vector3 dir);

        protected void KnockDown()
        {
            if (IsDown) return;
            IsDown = true;
            if (Game != null) Game.OnTargetDown(this);
        }

        /// <summary>Makes the target scorable again (pooled moving targets).</summary>
        protected void Revive() => IsDown = false;
    }
}
