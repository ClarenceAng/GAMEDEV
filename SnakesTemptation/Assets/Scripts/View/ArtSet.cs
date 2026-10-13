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
    }
}
