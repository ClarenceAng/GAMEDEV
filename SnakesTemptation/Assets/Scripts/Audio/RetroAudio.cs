using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// Plays the game's sounds (square-wave blips, a rumble, a roar, three music loops). They are WAV files under
    /// Assets/Audio, handed over by the controller through <see cref="Load"/>; nothing is synthesised while the game runs.
    ///
    /// Two rules keep it from ever being a surprise:
    ///  - It starts <see cref="Muted"/>. Nothing can make a sound (not even a direct call) until the controller unmutes it
    ///    when the game starts.
    ///  - Every clip is peak-normalised to a modest level in its file, and then scaled by <see cref="Master"/>, so the
    ///    overall level is a single slider (SnakeGameController.audioVolume).
    /// </summary>
    public sealed class RetroAudio : MonoBehaviour
    {
        AudioSource sfx, music, voice;
        SoundSet clips = new SoundSet();
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

        public IReadOnlyList<AudioClip> AllClips => clips.All;

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
        }

        /// <summary>The clips to play. A missing clip is simply silent.</summary>
        public void Load(SoundSet set) => clips = set ?? new SoundSet();

        void Update() => music.volume = Mathf.MoveTowards(music.volume, muted ? 0f : musicTarget * Master, Time.unscaledDeltaTime * 1.5f);

        public void Eat() => Play(clips.eat);
        public void Die() => Play(clips.die);
        public void Flip() => Play(clips.flip);
        public void Roar() => Play(clips.roar);
        public void Win() => Play(clips.win);
        public void Chime() => Play(clips.chime);     // a lantern was collected
        public void Dim() => Play(clips.dim);         // the glow ran out
        public void Jumpscare() => Play(clips.scare); // the apple monster gets you
        public void Respawn() => Play(clips.respawn); // back for another life
        public void Step(float volume01) => Play(clips.step, Mathf.Clamp01(volume01));
        public void Plant() => Play(clips.plant);     // a seed goes into the ground
        public void Grow() => Play(clips.grow);       // a tree shoots up
        public void Crunch() => Play(clips.crunch);   // Eve takes a bite
        public void Hiss() => Play(clips.hiss);
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
            if (muted || clip == null) return;
            PlaysRequested++;
            sfx.PlayOneShot(clip, gain * Master);
        }

        /// <summary>The gentle loop under the ending.</summary>
        public void PlayGardenMusic() => StartMusic(clips.musicGarden);

        public void PlayMusic(bool chase) => StartMusic(chase ? clips.musicChase : clips.musicRetro);

        void StartMusic(AudioClip clip)
        {
            if (muted || clip == null) return;
            PlaysRequested++;
            music.clip = clip;
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
    }
}
