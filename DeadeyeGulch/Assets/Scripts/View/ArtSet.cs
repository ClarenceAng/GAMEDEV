using System;
using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// Every material the game uses. They are real assets created by Tools > Deadeye Gulch > Build Scenes, so the shaders
    /// are guaranteed to be included in player builds.
    /// </summary>
    [Serializable]
    public sealed class ArtSet
    {
        [Header("Desert")]
        public Material sand;
        public Material mesa;
        public Material rock;
        public Material cactus;
        public Material skybox;

        [Header("Town")]
        public Material wood;
        public Material darkWood;
        public Material paint;
        public Material iron;
        public Material rope;

        [Header("Targets")]
        public Material can;
        public Material canBand;
        public Material bottleGreen;
        public Material bottleAmber;
        public Material banditCloth;
        public Material banditHat;
        public Material bandana;
        public Material skin;
        public Material townsfolkDress;
        public Material townsfolkHat;
        public Material ink;

        [Header("Gun and effects")]
        public Material gunMetal;
        public Material gunGrip;
        public Material brass;
        public Material tracer;
        public Material flash;
        public Material dust;

        public bool IsComplete => sand != null && wood != null && can != null && bottleGreen != null && banditCloth != null
                                  && gunMetal != null && tracer != null && flash != null;
    }
}
