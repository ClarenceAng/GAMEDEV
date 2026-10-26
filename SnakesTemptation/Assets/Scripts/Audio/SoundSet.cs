using UnityEngine;

// all the sounds in the game. the wav files are in Assets/Audio
[CreateAssetMenu(menuName = "Snake's Temptation/Sound Set", fileName = "SoundSet")]
public class SoundSet : ScriptableObject
{
    [Header("Sound effects")]
    public AudioClip eat;
    public AudioClip win;
    public AudioClip die;
    public AudioClip flip;
    public AudioClip roar;
    public AudioClip step;
    public AudioClip chime;    // got a lantern
    public AudioClip dim;      // lantern ran out
    public AudioClip scare;    // monster got you
    public AudioClip respawn;
    public AudioClip plant;
    public AudioClip grow;
    public AudioClip crunch;   // eve bites the apple
    public AudioClip hiss;

    [Header("Music")]
    public AudioClip musicRetro;
    public AudioClip musicChase;
    public AudioClip musicGarden;

    public AudioClip[] All
    {
        get
        {
            return new AudioClip[] { eat, win, die, flip, roar, step, chime, dim, scare, respawn, musicRetro, musicChase, plant, grow, crunch, hiss, musicGarden };
        }
    }

    public bool IsComplete
    {
        get
        {
            AudioClip[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) return false;
            }
            return true;
        }
    }
}
