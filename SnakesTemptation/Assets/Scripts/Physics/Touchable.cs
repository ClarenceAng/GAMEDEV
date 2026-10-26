using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>What a collider in the 3D world is, so the snake's head knows what it ran into.</summary>
    public enum Contact { Obstacle, Monster, Seed, Lantern }

    /// <summary>Tags a collider with its <see cref="Contact"/>.</summary>
    public sealed class Touchable : MonoBehaviour
    {
        public Contact kind;
    }
}
