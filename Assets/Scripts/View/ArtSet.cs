using System;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>Every asset the game needs. Filled in by the scene builder (Tools > Snake's Temptation > Build Snake Scene).</summary>
    [Serializable]
    public sealed class ArtSet
    {
        [Header("Retro look (URP Unlit): Game Boy LCD palette")]
        public Material retroBase;
        public Material retroTileA;
        public Material retroTileB;
        public Material retroSnake;
        public Material retroBorder;
        public Material retroSprite; // alpha-cutout; the 2D apple texture is assigned at runtime

        [Header("3D look (URP Lit)")]
        public Material ground;
        public Material tileA;
        public Material tileB;
        public Material plazaA;   // floor of the open areas
        public Material plazaB;
        public Material hedge;
        public Material trunk;
        public Material foliage;
        public Material snakeBody;
        public Material snakeHead;
        public Material eyeWhite;
        public Material pupil;
        public Material seed;      // the apple seed you collect to escape
        public Material seedBeam;  // the pillar of light that marks it
        public Material lantern;      // the lantern powerup's glowing orb
        public Material lanternBeam;  // its short beacon
    }
}
