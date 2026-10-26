using UnityEngine;

// what kind of thing a collider is
public enum Contact { Obstacle, Monster, Seed, Lantern }

// put this on a collider so the snake head knows what it touched
public class Touchable : MonoBehaviour
{
    public Contact kind;
}
