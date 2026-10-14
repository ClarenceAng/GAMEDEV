using System.Collections.Generic;
using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// All sound is synthesised at startup (noise bursts, ringing partials, clicks), so the game needs no audio files.
    /// Swap in real clips by replacing the fields below if you buy a pack later.
    ///
    /// Same two rules as the Snake project:
    ///  - It starts <see cref="Muted"/> and nothing can play until the game unmutes it when a round starts.
    ///  - Every clip is peak-normalised and scaled by <see cref="Master"/>, so the overall level is one slider
    ///    (KnockdownGame.audioVolume).
    /// Each sound is pitched slightly at random so rapid fire doesn't sound like a machine.
    /// </summary>
    public sealed class WesternAudio : MonoBehaviour
    {
        const int SR = 44100;
        const int Voices = 8;

        AudioSource[] voices;
        int nextVoice;
        AudioClip gunshot, clang, glass, thunk, reload, reloadDone, penalty, bell;
        bool muted = true;
        System.Random rnd = new System.Random(11);

        /// <summary>0..1 overall volume.</summary>
        public float Master { get; set; } = 0.5f;

        public bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                if (value && voices != null) foreach (var v in voices) v.Stop();
            }
        }

        /// <summary>How many sounds have actually been started (lets tests prove silence is silence).</summary>
        public int PlaysRequested { get; private set; }

        public IReadOnlyList<AudioClip> AllClips { get; private set; }

        void Awake()
        {
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                voices[i] = s;
            }

            gunshot = MakeGunshot();
            clang = Ring("clang", 0.5f, 0.6f, new[] { 1180f, 1730f, 2610f, 3420f }, 9f, 0.25f);
            glass = MakeGlass();
            thunk = MakeThunk();
            reload = Clicks("reload", new[] { 0f, 0.12f, 0.24f, 0.36f, 0.48f, 0.6f }, 0.35f);
            reloadDone = Clicks("reloadDone", new[] { 0f, 0.05f }, 0.6f);
            penalty = MakePenalty();
            bell = Ring("bell", 1.6f, 0.55f, new[] { 660f, 1320f, 1980f, 2770f }, 2.2f, 0f);
            AllClips = new[] { gunshot, clang, glass, thunk, reload, reloadDone, penalty, bell };
        }

        public void Gunshot() => Play(gunshot, 1f, 0.06f);
        public void Clang() => Play(clang, 0.7f, 0.12f);
        public void Glass() => Play(glass, 0.8f, 0.12f);
        public void Thunk() => Play(thunk, 0.9f, 0.08f);
        public void Reload() => Play(reload, 0.5f, 0f);
        public void ReloadDone() => Play(reloadDone, 0.6f, 0f);
        public void Penalty() => Play(penalty, 0.8f, 0f);
        public void Bell() => Play(bell, 0.7f, 0f);

        void Play(AudioClip clip, float gain, float pitchJitter)
        {
            if (muted) return;
            PlaysRequested++;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            v.pitch = 1f + ((float)rnd.NextDouble() * 2f - 1f) * pitchJitter;
            v.clip = clip;
            v.volume = gain * Master;
            v.Play();
        }

        // ---------------------------------------------------------------- synthesis

        static AudioClip Clip(string name, float[] data, float peak = 0.9f)
        {
            float max = 0f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            if (max > 1e-5f)
            {
                float k = peak / max;
                for (int i = 0; i < data.Length; i++) data[i] *= k;
            }
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        // A sharp crack (white noise, very fast decay) over a low boom, with a little echo off the canyon.
        static AudioClip MakeGunshot()
        {
            int n = (int)(1.1f * SR);
            var d = new float[n];
            var r = new System.Random(1);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(r.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.25f;                                  // darker noise for the body
                float crack = noise * Mathf.Exp(-t * 60f);
                float body = lp * Mathf.Exp(-t * 9f) * 1.6f;
                float boom = Mathf.Sin(2f * Mathf.PI * 70f * t * (1f - t)) * Mathf.Exp(-t * 14f) * 0.9f;
                d[i] = crack + body + boom;
            }
            int echo = (int)(0.32f * SR);
            for (int i = n - 1; i >= echo; i--) d[i] += d[i - echo] * 0.22f;
            return Clip("gunshot", d);
        }

        // A few inharmonic partials ringing down: tin can, steel, bells.
        static AudioClip Ring(string name, float secs, float peak, float[] partials, float decay, float noiseHit)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var r = new System.Random(2);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float s = 0f;
                for (int p = 0; p < partials.Length; p++)
                    s += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * decay * (1f + p * 0.5f)) / (1f + p * 0.6f);
                s += (float)(r.NextDouble() * 2.0 - 1.0) * noiseHit * Mathf.Exp(-t * 80f);
                d[i] = s;
            }
            return Clip(name, d, peak);
        }

        // Bright noise with random tinkling pings.
        static AudioClip MakeGlass()
        {
            int n = (int)(0.7f * SR);
            var d = new float[n];
            var r = new System.Random(4);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(r.NextDouble() * 2.0 - 1.0);
                float hp = noise - prev;                                     // high-passed: glassy hiss
                prev = noise;
                d[i] = hp * Mathf.Exp(-t * 14f) * 0.8f;
            }
            for (int k = 0; k < 14; k++)
            {
                int start = (int)(r.NextDouble() * 0.4 * SR);
                float f = 2500f + (float)r.NextDouble() * 4500f;
                for (int i = 0; i < 0.12f * SR && start + i < n; i++)
                {
                    float t = i / (float)SR;
                    d[start + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 40f) * 0.35f;
                }
            }
            return Clip("glass", d);
        }

        // A plywood board slapping down: low knock plus a short burst of dull noise.
        static AudioClip MakeThunk()
        {
            int n = (int)(0.35f * SR);
            var d = new float[n];
            var r = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += ((float)(r.NextDouble() * 2.0 - 1.0) - lp) * 0.1f;
                d[i] = Mathf.Sin(2f * Mathf.PI * (180f - 80f * t) * t) * Mathf.Exp(-t * 18f) + lp * 2.5f * Mathf.Exp(-t * 30f);
            }
            return Clip("thunk", d, 0.85f);
        }

        // Metallic clicks at the given times (cylinder turning, rounds going in).
        static AudioClip Clicks(string name, float[] times, float peak)
        {
            int n = (int)((times[times.Length - 1] + 0.08f) * SR);
            var d = new float[n];
            var r = new System.Random(6);
            foreach (float at in times)
            {
                int start = (int)(at * SR);
                for (int i = 0; i < 0.03f * SR && start + i < n; i++)
                {
                    float t = i / (float)SR;
                    d[start + i] += ((float)(r.NextDouble() * 2.0 - 1.0) * 0.5f + Mathf.Sin(2f * Mathf.PI * 3200f * t)) * Mathf.Exp(-t * 220f);
                }
            }
            return Clip(name, d, peak);
        }

        // A sour two-note "wah-wah" for shooting a townsperson.
        static AudioClip MakePenalty()
        {
            int n = (int)(0.6f * SR);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = t < 0.25f ? 220f : 165f;
                ph += f * (1f + 0.02f * Mathf.Sin(2f * Mathf.PI * 6f * t)) / SR;
                float saw = (float)(2.0 * (ph % 1.0) - 1.0);
                float local = t < 0.25f ? t : t - 0.25f;
                d[i] = saw * Mathf.Min(1f, local * 40f) * Mathf.Exp(-local * 3f);
            }
            return Clip("penalty", d, 0.5f);
        }
    }
}
