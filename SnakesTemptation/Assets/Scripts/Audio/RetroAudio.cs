using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// All sound is synthesised at startup (square-wave blips and a music loop), so the game needs no audio files. Swap in
    /// real clips by replacing the fields below if you buy a pack later.
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

        AudioSource sfx, music;
        AudioClip eat, die, musicRetro;
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

            eat = Notes("eat", 0.07f, 880f, 1318f);
            die = Sweep("die", 0.75f, 440f, 38f, true, 0.18f);
            musicRetro = RetroLoop();
            AllClips = new[] { eat, die, musicRetro };
        }

        void Update() => music.volume = Mathf.MoveTowards(music.volume, muted ? 0f : musicTarget * Master, Time.unscaledDeltaTime * 1.5f);

        public void Eat() => Play(eat);
        public void Die() => Play(die);

        void Play(AudioClip clip, float gain = 1f)
        {
            if (muted) return;
            PlaysRequested++;
            sfx.PlayOneShot(clip, gain * Master);
        }

        public void PlayMusic()
        {
            if (muted) return;
            PlaysRequested++;
            music.clip = musicRetro;
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
    }
}
