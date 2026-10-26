using UnityEngine;

// plays all the sounds and music. it listens to the game events so the game never has to tell it what to play.
// it starts muted and nothing plays until the game unmutes it (so the title screen is quiet)
public class RetroAudio : MonoBehaviour
{
    public float Master = 0.4f;     // overall volume
    public bool Muted = true;
    public int PlaysRequested;      // how many sounds were started (the tests check this)
    public int VoicesPlayed;

    AudioSource sfx;
    AudioSource music;
    AudioSource voice;
    SoundSet clips;
    GameEvents events;
    float musicTarget;

    public AudioClip[] AllClips
    {
        get { return clips.All; }
    }

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.volume = 0f;

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;

        if (clips == null) clips = ScriptableObject.CreateInstance<SoundSet>();
    }

    public void Load(SoundSet set)
    {
        if (set != null) clips = set;
        else clips = ScriptableObject.CreateInstance<SoundSet>();
    }

    public void SetMuted(bool mute)
    {
        Muted = mute;
        if (mute)
        {
            // stop everything
            musicTarget = 0f;
            music.Stop();
            music.volume = 0f;
            sfx.Stop();
            voice.Stop();
        }
    }

    public void Listen(GameEvents gameEvents)
    {
        if (gameEvents == null || !gameEvents.IsComplete) return;
        events = gameEvents;
        events.retroStarted.Register(OnRetroStarted);
        events.pickupEaten.Register(Eat);
        events.worldFlipped.Register(OnWorldFlipped);
        events.monsterRose.Register(Roar);
        events.chaseStarted.Register(OnChaseStarted);
        events.lanternCollected.Register(Chime);
        events.lanternExpired.Register(Dim);
        events.snakeCrashed.Register(OnSnakeCrashed);
        events.snakeDevoured.Register(OnSnakeDevoured);
        events.snakeRespawned.Register(OnSnakeRespawned);
        events.gameWon.Register(OnGameWon);
        events.endingStarted.Register(OnEndingStarted);
        events.seedPlanted.Register(Plant);
        events.treeGrew.Register(Grow);
        events.serpentHissed.Register(Hiss);
        events.appleBitten.Register(Crunch);
    }

    void OnDestroy()
    {
        if (events == null) return;
        events.retroStarted.Unregister(OnRetroStarted);
        events.pickupEaten.Unregister(Eat);
        events.worldFlipped.Unregister(OnWorldFlipped);
        events.monsterRose.Unregister(Roar);
        events.chaseStarted.Unregister(OnChaseStarted);
        events.lanternCollected.Unregister(Chime);
        events.lanternExpired.Unregister(Dim);
        events.snakeCrashed.Unregister(OnSnakeCrashed);
        events.snakeDevoured.Unregister(OnSnakeDevoured);
        events.snakeRespawned.Unregister(OnSnakeRespawned);
        events.gameWon.Unregister(OnGameWon);
        events.endingStarted.Unregister(OnEndingStarted);
        events.seedPlanted.Unregister(Plant);
        events.treeGrew.Unregister(Grow);
        events.serpentHissed.Unregister(Hiss);
        events.appleBitten.Unregister(Crunch);
    }

    void OnRetroStarted() { PlayMusic(false); }
    void OnWorldFlipped() { StopMusic(); Flip(); }
    void OnChaseStarted() { PlayMusic(true); }
    void OnSnakeCrashed() { StopMusic(); Die(); }
    void OnSnakeDevoured() { StopMusic(); Jumpscare(); }
    void OnSnakeRespawned() { Respawn(); PlayMusic(true); }
    void OnGameWon() { StopMusic(); Win(); }
    void OnEndingStarted() { StopMusic(); PlayGardenMusic(); }

    void Update()
    {
        // fade the music in and out
        float target = musicTarget * Master;
        if (Muted) target = 0f;
        music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * 1.5f);
    }

    public void Eat() { Play(clips.eat, 1f); }
    public void Die() { Play(clips.die, 1f); }
    public void Flip() { Play(clips.flip, 1f); }
    public void Roar() { Play(clips.roar, 1f); }
    public void Win() { Play(clips.win, 1f); }
    public void Chime() { Play(clips.chime, 1f); }
    public void Dim() { Play(clips.dim, 1f); }
    public void Jumpscare() { Play(clips.scare, 1f); }
    public void Respawn() { Play(clips.respawn, 1f); }
    public void Plant() { Play(clips.plant, 1f); }
    public void Grow() { Play(clips.grow, 1f); }
    public void Crunch() { Play(clips.crunch, 1f); }
    public void Hiss() { Play(clips.hiss, 1f); }

    public void Step(float volume)
    {
        Play(clips.step, Mathf.Clamp01(volume));
    }

    void Play(AudioClip clip, float volume)
    {
        if (Muted || clip == null) return;
        PlaysRequested++;
        sfx.PlayOneShot(clip, volume * Master);
    }

    // voice acting for the ending (we don't have any yet but it works if you add clips)
    public void PlayVoice(AudioClip clip)
    {
        if (Muted || clip == null) return;
        PlaysRequested++;
        VoicesPlayed++;
        voice.Stop();
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(Master * 2f);
        voice.Play();
    }

    public void StopVoice()
    {
        voice.Stop();
    }

    public void PlayMusic(bool chase)
    {
        if (chase) StartMusic(clips.musicChase);
        else StartMusic(clips.musicRetro);
    }

    public void PlayGardenMusic()
    {
        StartMusic(clips.musicGarden);
    }

    void StartMusic(AudioClip clip)
    {
        if (Muted || clip == null) return;
        PlaysRequested++;
        music.clip = clip;
        musicTarget = 1f;
        music.volume = 0f; // it fades in in Update
        music.Play();
    }

    public void StopMusic()
    {
        musicTarget = 0f;
    }
}
