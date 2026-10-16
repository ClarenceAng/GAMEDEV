using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// One perspective camera does both looks.
    ///
    /// Retro (<see cref="Blend"/> = 0): it hangs very high with a very narrow field of view, which is visually
    /// indistinguishable from the flat orthographic view of the retro game.
    ///
    /// Chase (<see cref="Blend"/> = 1): a third-person camera close behind the snake, tilted down, that swings around
    /// corners with it and looks a little ahead of the head.
    ///
    /// Blending between them dollies in while widening the field of view (a dolly-zoom) and tilts down, so the flip feels
    /// like falling into the board. No projection-matrix tricks needed.
    /// </summary>
    sealed class CameraRig
    {
        const float RetroDistance = 90f;
        const float ChaseFov = 68f;
        const float ChasePitch = 32f;       // degrees below horizontal
        const float ChaseRadius = 5.6f;     // half-height of the view slice around the snake: smaller = closer
        const float LookAhead = 2.2f;       // cells in front of the head the camera aims at

        readonly Camera cam;
        readonly float retroW, retroH;
        Vector3 focus, focusVel;
        float yaw, yawVel;
        float impulse;
        bool haveYaw, snap;

        public float Blend;                 // 0 = flat retro view, 1 = third-person chase
        public float Shake;                 // continuous shake amount (0..1)
        public Vector3 FollowPos;           // world position of the snake's head
        public float FovKick;               // degrees added to the field of view and then relaxed (the jumpscare's lens punch)
        public Vector3 FollowHeading = Vector3.forward; // horizontal unit vector the snake is heading in

        public CameraRig(Camera cam, int retroWidth, int retroHeight)
        {
            this.cam = cam;
            retroW = retroWidth;
            retroH = retroHeight;
        }

        public void Impulse(float amount) => impulse = Mathf.Max(impulse, amount);

        /// <summary>Jump straight to wherever the snake is now instead of swooping there (used when it respawns).</summary>
        public void Snap()
        {
            snap = true;
            haveYaw = false;
            yawVel = 0f;
        }

        /// <summary>The camera's current heading (degrees around the vertical axis), smoothed through turns.</summary>
        public float Yaw => yaw;

        public void Tick(float dt)
        {
            float s = Mathf.Clamp01(Blend);
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float halfH = retroH * 0.5f + 1.5f;
            float halfW = retroW * 0.5f + 1.5f;
            float r0 = Mathf.Max(halfH, halfW / aspect) * 1.06f;   // retro: whole board + bezel visible

            float fov0 = 2f * Mathf.Atan(r0 / RetroDistance) * Mathf.Rad2Deg;
            float fov = Mathf.Lerp(fov0, ChaseFov, s);
            float radius = Mathf.Lerp(r0, ChaseRadius, s);
            float dist = radius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float pitch = Mathf.Lerp(90f, ChasePitch, s) * Mathf.Deg2Rad;

            // Swing around behind the snake as it turns.
            Vector3 h = FollowHeading.sqrMagnitude > 0.01f ? FollowHeading.normalized : Vector3.forward;
            float targetYaw = Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg;
            if (!haveYaw || dt <= 0f) { yaw = targetYaw; haveYaw = true; }
            else yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVel, 0.22f);
            Vector3 fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

            Vector3 want = Vector3.Lerp(Vector3.zero, FollowPos + fwd * LookAhead + Vector3.up * 0.4f, s);
            if (snap) { focus = want; focusVel = Vector3.zero; }
            else focus = dt > 0f ? Vector3.SmoothDamp(focus, want, ref focusVel, 0.10f, 200f, dt) : want;
            snap = false;

            Vector3 pos = focus + (-fwd * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * dist;
            float amp = Mathf.Max(Shake, impulse) * 0.18f;
            if (amp > 0f) pos += Random.insideUnitSphere * amp;
            impulse = Mathf.MoveTowards(impulse, 0f, dt * 2.2f);

            // Straight down needs a "which way is up on screen" hint; once tilted, plain world-up is right.
            Vector3 upHint = Vector3.Slerp(Vector3.forward, Vector3.up, MathX.Smooth(0.1f, 0.5f, s));
            if (dt > 0f) FovKick = Mathf.Lerp(FovKick, 0f, 1f - Mathf.Exp(-5f * dt));
            cam.fieldOfView = fov + FovKick;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, upHint));
        }
    }
}
