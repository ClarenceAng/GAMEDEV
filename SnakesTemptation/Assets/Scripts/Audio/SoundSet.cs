using System;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// Every sound the game plays, as an asset (Assets/ScriptableObjects/SoundSet.asset) that points at the WAV files under
    /// Assets/Audio (Sfx and Music). Swapping a sound means changing this asset, not the code or the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Snake's Temptation/Sound Set", fileName = "SoundSet")]
    public sealed class SoundSet : ScriptableObject
    {
        [Header("Effects (Assets/Audio/Sfx)")]
        public AudioClip eat;
        public AudioClip win;
        public AudioClip die;
        public AudioClip flip;
        public AudioClip roar;
        public AudioClip step;
        public AudioClip chime;    // a lantern was collected
        public AudioClip dim;      // the glow ran out
        public AudioClip scare;    // the apple monster gets you
        public AudioClip respawn;  // back for another life
        public AudioClip plant;    // a seed goes into the ground
        public AudioClip grow;     // a tree shoots up
        public AudioClip crunch;   // Eve takes a bite
        public AudioClip hiss;

        [Header("Music loops (Assets/Audio/Music)")]
        public AudioClip musicRetro;
        public AudioClip musicChase;
        public AudioClip musicGarden;

        /// <summary>Every clip (null where one is missing).</summary>
        public AudioClip[] All => new[] { eat, win, die, flip, roar, step, chime, dim, scare, respawn, musicRetro, musicChase, plant, grow, crunch, hiss, musicGarden };

        public bool IsComplete => Array.TrueForAll(All, c => c != null);
    }
}
