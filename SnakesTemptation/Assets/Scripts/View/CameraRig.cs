using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The retro view: a perspective camera hanging very high above the board with a very narrow field of view, which is
    /// visually indistinguishable from a flat orthographic view. The field of view is fitted so the whole board and its
    /// border are on screen at any aspect ratio.
    /// </summary>
    sealed class CameraRig
    {
        const float RetroDistance = 90f;

        readonly Camera cam;
        readonly float retroW, retroH;
        float impulse;

        public CameraRig(Camera cam, int retroWidth, int retroHeight)
        {
            this.cam = cam;
            retroW = retroWidth;
            retroH = retroHeight;
        }

        /// <summary>A brief camera shake (a crash).</summary>
        public void Impulse(float amount) => impulse = Mathf.Max(impulse, amount);

        public void Tick(float dt)
        {
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float halfH = retroH * 0.5f + 1.5f;
            float halfW = retroW * 0.5f + 1.5f;
            float r = Mathf.Max(halfH, halfW / aspect) * 1.06f;   // whole board + bezel visible

            Vector3 pos = Vector3.up * RetroDistance;
            float amp = impulse * 0.18f;
            if (amp > 0f) pos += Random.insideUnitSphere * amp;
            impulse = Mathf.MoveTowards(impulse, 0f, dt * 2.2f);

            // Straight down needs a "which way is up on screen" hint: +Z is up.
            cam.fieldOfView = 2f * Mathf.Atan(r / RetroDistance) * Mathf.Rad2Deg;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.zero - pos, Vector3.forward));
        }
    }
}
