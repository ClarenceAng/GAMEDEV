using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// All sound is synthesised at startup (square-wave blips, a rumble, a roar, two music loops), so the game needs
    /// no audio files. Swap in real clips by replacing the fields below if you buy a pack later.
    ///
    /// Two rules keep it from ever being a surprise:
    ///  - It starts <see cref="Muted"/>. Nothing can make a sound (not even a direct call) until the controller unmutes it
    ///    when the game starts.
    ///  - Every clip is peak-normalised to a modest level and then scaled by <see cref="Master"/>, so relative loudness is
    ///    decided in one place and the overall level is a single slider (SnakeGameController.audioVolume).
    /// </summary>
    public sealed class RetroAudio : MonoBehaviour
    {
        const int SR = 44100;

        // Peak level of each clip before the master volume (full scale = 1).
        const float SfxPeak = 0.55f;
        const float MusicRetroPeak = 0.35f;
        const float MusicChasePeak = 0.40f;

        AudioSource sfx, music, voice;
        AudioClip eat, die, flip, roar, win, step, chime, dim, scare, respawn, musicRetro, musicChase;
        AudioClip plant, grow, crunch, hiss, musicGarden;
        float musicTarget;
        bool muted = true;

        /// <summary>0..1 overall volume. Applied on top of the per-clip levels.</summary>
        public float Master { get; set; } = 0.4f;

        /// <summary>While muted nothing plays. Muting also cuts anything already playing.</summary>
        public bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                if (value) Silence();
            }
        }

        /// <summary>How many sounds have actually been started. Lets tests prove that silence is silence.</summary>
        public int PlaysRequested { get; private set; }

        public IReadOnlyList<AudioClip> AllClips { get; private set; }

        /// <summary>How many voice clips have been started (the ending's voice acting).</summary>
        public int VoicesPlayed { get; private set; }

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0f;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;

            eat = Notes("eat", 0.07f, 880f, 1318f);
            win = Notes("win", 0.11f, 523f, 659f, 784f, 1047f, 784f, 1047f);
            die = Sweep("die", 0.75f, 440f, 38f, true, 0.18f);
            flip = Rumble("flip", 3.4f);
            roar = Roar("roar", 1.3f);
            step = Thump("step", 0.16f);
            chime = Notes("chime", 0.07f, 784f, 988f, 1319f, 1760f);
            dim = Notes("dim", 0.16f, 523f, 392f, 262f);
            scare = Scream("scare", 1.4f);
            respawn = Notes("respawn", 0.1f, 262f, 330f, 392f, 523f);
            musicRetro = RetroLoop();
            musicChase = ChaseLoop();
            plant = Plant("plant", 0.9f);
            grow = Grow("grow", 2.2f);
            crunch = Crunch("crunch", 0.55f);
            hiss = Hiss("hiss", 0.8f);
            musicGarden = GardenLoop();
            AllClips = new[] { eat, win, die, flip, roar, step, chime, dim, scare, respawn, musicRetro, musicChase, plant, grow, crunch, hiss, musicGarden };
        }

        void Update() => music.volume = Mathf.MoveTowards(music.volume, muted ? 0f : musicTarget * Master, Time.unscaledDeltaTime * 1.5f);

        public void Eat() => Play(eat);
        public void Die() => Play(die);
        public void Flip() => Play(flip);
        public void Roar() => Play(roar);
        public void Win() => Play(win);
        public void Chime() => Play(chime);   // a lantern was collected
        public void Dim() => Play(dim);       // the glow ran out
        public void Jumpscare() => Play(scare); // the apple monster gets you
        public void Respawn() => Play(respawn); // back for another life
        public void Step(float volume01) => Play(step, Mathf.Clamp01(volume01));
        public void Plant() => Play(plant);     // a seed goes into the ground
        public void Grow() => Play(grow);       // a tree shoots up
        public void Crunch() => Play(crunch);   // Eve takes a bite
        public void Hiss() => Play(hiss);
        public void StopVoice() { if (voice != null) voice.Stop(); }

        /// <summary>Plays a recorded voice line (for the ending). Only the master volume applies: the clip is as loud as it was recorded.</summary>
        public void PlayVoice(AudioClip clip)
        {
            if (muted || clip == null) return;
            PlaysRequested++;
            VoicesPlayed++;
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(Master * 2f);
            voice.Play();
        }

        void Play(AudioClip clip, float gain = 1f)
        {
            if (muted) return;
            PlaysRequested++;
            sfx.PlayOneShot(clip, gain * Master);
        }

        /// <summary>The gentle loop under the ending.</summary>
        public void PlayGardenMusic()
        {
            if (muted) return;
            PlaysRequested++;
            music.clip = musicGarden;
            musicTarget = 1f;
            music.volume = 0f;
            music.Play();
        }

        public void PlayMusic(bool chase)
        {
            if (muted) return;
            PlaysRequested++;
            music.clip = chase ? musicChase : musicRetro;
            musicTarget = 1f;
            music.volume = 0f; // fades in
            music.Play();
        }

        public void StopMusic() => musicTarget = 0f;

        void Silence()
        {
            musicTarget = 0f;
            if (music != null) { music.Stop(); music.volume = 0f; }
            if (sfx != null) sfx.Stop();
            if (voice != null) voice.Stop();
        }

        // ---------------------------------------------------------------- synthesis

        // Scales the samples so the loudest one sits exactly at `peak`.
        static AudioClip Clip(string name, float[] data, float peak)
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

        static float Square(double phase) => (phase % 1.0) < 0.5 ? 1f : -1f;
        static float Saw(double phase) => (float)(2.0 * (phase % 1.0) - 1.0);

        // The jumpscare sting: a hard noise smack, a sub-bass boom and a piercing, wobbling screech that climbs and then sags.
        // It is soft-clipped so it sounds harsh and dense, but it is peak-normalised like every other clip: it startles by being
        // sudden (the music has just stopped), not by being louder than the volume you chose.
        static AudioClip Scream(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(13);
            double p1 = 0, p2 = 0, p3 = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n, t = i / (float)SR;
                float rise = Mathf.Min(1f, k * 3f);
                float f = Mathf.Lerp(620f, 1500f, rise) * (1f - 0.28f * Mathf.Max(0f, k - 0.5f)) * (1f + 0.045f * Mathf.Sin(t * 61f));
                p1 += f / SR;
                p2 += f * 1.51f / SR;
                p3 += f * 0.5f / SR;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.35f;
                float smack = k < 0.07f ? 1f - k / 0.07f : 0f;
                float env = Mathf.Min(1f, k * 60f) * (k < 0.5f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - (k - 0.5f) / 0.5f), 1.6f));
                float boom = Mathf.Sin(2f * Mathf.PI * 46f * t) * Mathf.Exp(-t * 4.5f);
                float s = (Saw(p1) * 0.5f + Saw(p2) * 0.35f + Square(p3) * 0.25f) * env + noise * smack * 1.3f + lp * 0.3f * env + boom * 0.9f;
                d[i] = (float)System.Math.Tanh(s * 2.2f);
            }
            return Clip(name, d, 0.58f);
        }

        // A run of square-wave notes with a quick decay each: the classic blip.
        static AudioClip Notes(string name, float noteDur, params float[] freqs)
        {
            int per = (int)(noteDur * SR);
            var d = new float[per * freqs.Length];
            double ph = 0;
            for (int n = 0; n < freqs.Length; n++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)per;
                    ph += freqs[n] / SR;
                    d[n * per + i] = Square(ph) * (1f - t) * (1f - t * 0.3f);
                }
            return Clip(name, d, SfxPeak);
        }

        static AudioClip Sweep(string name, float secs, float f0, float f1, bool square, float vol)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(3);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                ph += Mathf.Lerp(f0, f1, k) / SR;
                float s = square ? Square(ph) : Saw(ph);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.4f * k;
                d[i] = (s + noise) * vol * (1f - k);
            }
            return Clip(name, d, SfxPeak);
        }

        // Rising dread: detuned saws sweeping up under swelling noise.
        static AudioClip Rumble(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(11);
            double p1 = 0, p2 = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float f = Mathf.Lerp(48f, 520f, k * k);
                p1 += f / SR;
                p2 += f * 1.013f / SR;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.08f;
                float env = Mathf.Min(1f, k * 3f) * (1f - Mathf.Pow(Mathf.Max(0f, k - 0.85f) / 0.15f, 2f));
                float trem = 0.75f + 0.25f * Mathf.Sin(k * 60f);
                d[i] = (Saw(p1) * 0.13f + Saw(p2) * 0.13f + lp * 0.9f * k) * env * trem;
            }
            return Clip(name, d, SfxPeak);
        }

        static AudioClip Roar(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(5);
            double ph = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                ph += Mathf.Lerp(95f, 38f, k) / SR;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.15f;
                float env = Mathf.Min(1f, k * 12f) * Mathf.Pow(1f - k, 1.4f);
                d[i] = (Saw(ph) * 0.35f + lp * 0.7f) * env * (0.8f + 0.2f * Mathf.Sin(k * 90f));
            }
            return Clip(name, d, SfxPeak);
        }

        static AudioClip Thump(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                ph += Mathf.Lerp(80f, 32f, k) / SR;
                d[i] = Mathf.Sin((float)(ph * Mathf.PI * 2.0)) * Mathf.Exp(-k * 6f);
            }
            return Clip(name, d, SfxPeak);
        }

        // A soft thump of earth and three little chimes: a seed goes in.
        static AudioClip Plant(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float thump = Mathf.Sin(2f * Mathf.PI * (95f - 40f * Mathf.Min(1f, t * 5f)) * t) * Mathf.Exp(-t * 14f);
                float chimes = 0f;
                float[] f = { 1568f, 2093f, 2637f };
                for (int k = 0; k < 3; k++)
                {
                    float ct = t - 0.12f - k * 0.1f;
                    if (ct > 0f) chimes += Mathf.Sin(2f * Mathf.PI * f[k] * ct) * Mathf.Exp(-ct * 7f) * 0.25f;
                }
                d[i] = thump * 0.9f + chimes;
            }
            return Clip(name, d, SfxPeak);
        }

        // A shimmering rise with a breath of wind under it: a tree grows.
        static AudioClip Grow(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(21);
            double p1 = 0, p2 = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float f = Mathf.Lerp(220f, 1320f, k * k);
                p1 += f / SR;
                p2 += f * 1.5f / SR;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.04f;
                float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                float trem = 0.7f + 0.3f * Mathf.Sin(k * 70f);
                d[i] = (Mathf.Sin((float)(p1 * Mathf.PI * 2.0)) * 0.5f + Mathf.Sin((float)(p2 * Mathf.PI * 2.0)) * 0.25f + lp * 2.2f) * env * trem;
            }
            return Clip(name, d, SfxPeak);
        }

        // Two juicy bites: short bursts of crackling noise with a low thump.
        static AudioClip Crunch(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(8);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float s = 0f;
                float[] starts = { 0f, 0.2f };
                foreach (float st in starts)
                {
                    float bt = t - st;
                    if (bt < 0f || bt > 0.14f) continue;
                    float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.5f;
                    float crack = (rnd.NextDouble() < 0.12 ? 1f : 0.25f) * (noise - lp * 0.5f);
                    s += crack * Mathf.Exp(-bt * 28f) + Mathf.Sin(2f * Mathf.PI * 120f * bt) * Mathf.Exp(-bt * 30f) * 0.6f;
                }
                d[i] = s;
            }
            return Clip(name, d, SfxPeak);
        }

        // "Ssssss": hushed, high noise that swells and fades.
        static AudioClip Hiss(string name, float secs)
        {
            int n = (int)(secs * SR);
            var d = new float[n];
            var rnd = new System.Random(34);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float hp = noise - prev * 0.85f; // a crude high-pass
                prev = noise;
                float env = Mathf.Sin(Mathf.Pow(k, 0.7f) * Mathf.PI);
                d[i] = hp * env;
            }
            return Clip(name, d, 0.3f);
        }

        // A music-box lullaby for the garden: slow arpeggios over soft chords, 6.4 s, seamless.
        static AudioClip GardenLoop()
        {
            const float bar = 1.6f;
            int n = (int)(4 * bar * SR);
            var d = new float[n];
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f },   // C
                new[] { 220.00f, 261.63f, 329.63f },   // Am
                new[] { 174.61f, 220.00f, 261.63f },   // F
                new[] { 196.00f, 246.94f, 293.66f },   // G
            };
            for (int b = 0; b < 4; b++)
            {
                var ch = chords[b];
                int start = (int)(b * bar * SR), len = (int)(bar * SR);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)SR, w = Mathf.Sin(Mathf.PI * i / len);
                    float pad = 0f;
                    foreach (float f in ch) pad += Mathf.Sin(2f * Mathf.PI * f * 0.5f * (start + i) / SR) * 0.5f;
                    d[(start + i) % n] += pad * w * w * 0.10f;
                }
                // eight plucked notes per bar
                for (int k = 0; k < 8; k++)
                {
                    float f = ch[(k < 4 ? k : 7 - k) % 3] * (k % 4 == 3 ? 4f : 2f);
                    int ns = start + (int)(k * bar / 8f * SR), nl = (int)(0.9f * SR);
                    for (int i = 0; i < nl; i++)
                    {
                        float t = i / (float)SR;
                        float env = Mathf.Exp(-t * 4.5f) * Mathf.Min(1f, t * 200f);
                        float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) * Mathf.Exp(-t * 9f);
                        d[(ns + i) % n] += s * env * 0.16f;
                    }
                }
            }
            return Clip("musicGarden", d, 0.33f);
        }

        // 16 eighth notes at 150 bpm (3.2 s): a bouncy C-minor arpeggio over a pulsing bass.
        static AudioClip RetroLoop()
        {
            float[] lead = { 523.25f, 622.25f, 783.99f, 622.25f, 523.25f, 622.25f, 698.46f, 622.25f, 523.25f, 622.25f, 783.99f, 932.33f, 783.99f, 622.25f, 523.25f, 392f };
            float[] bass = { 130.81f, 0, 130.81f, 0, 155.56f, 0, 155.56f, 0, 130.81f, 0, 130.81f, 0, 98f, 0, 98f, 0 };
            int per = (int)(0.2f * SR);
            var d = new float[per * 16];
            double pl = 0, pb = 0;
            for (int n = 0; n < 16; n++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)per;
                    pl += lead[n] / SR;
                    pb += bass[n] / SR;
                    float l = Square(pl) * 0.07f * Mathf.Exp(-t * 3.5f);
                    float b = bass[n] > 0f ? Square(pb) * 0.07f * (1f - t * 0.6f) : 0f;
                    d[n * per + i] = l + b;
                }
            return Clip("musicRetro", d, MusicRetroPeak);
        }

        // 3.2 s drone with a heartbeat. Frequencies are whole cycles per loop so it repeats without a click.
        static AudioClip ChaseLoop()
        {
            int n = (int)(3.2f * SR);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float drone = Saw(55.0 * t) * 0.10f + Saw(58.125 * t) * 0.10f;
                float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.625f * t);
                float beat = 0f;
                for (int b = 0; b < 4; b++)
                {
                    float bt = t - b * 0.8f;
                    if (bt >= 0f && bt < 0.4f) beat += Mathf.Sin(2f * Mathf.PI * 52f * bt) * Mathf.Exp(-bt * 14f) * 0.55f;
                    float bt2 = bt - 0.22f;
                    if (bt2 >= 0f && bt2 < 0.3f) beat += Mathf.Sin(2f * Mathf.PI * 46f * bt2) * Mathf.Exp(-bt2 * 16f) * 0.4f;
                }
                d[i] = drone * swell + beat;
            }
            return Clip("musicChase", d, MusicChasePeak);
        }
    }
}
