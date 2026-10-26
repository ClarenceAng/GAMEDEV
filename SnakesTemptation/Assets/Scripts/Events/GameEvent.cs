using System;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// An event channel (event broadcasting). Whoever raises it and whoever listens to it only share this asset, never a
    /// reference to each other: the game raises "the snake crashed", and the audio and the camera react to it. The channels
    /// live under Assets/ScriptableObjects/Events and are grouped by <see cref="GameEvents"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Snake's Temptation/Game Event", fileName = "GameEvent")]
    public sealed class GameEvent : ScriptableObject
    {
        event Action Raised;

        /// <summary>Calls every listener.</summary>
        public void Raise() => Raised?.Invoke();

        public void Register(Action listener) => Raised += listener;

        /// <summary>Listeners must unregister when they are destroyed: the asset outlives every scene.</summary>
        public void Unregister(Action listener) => Raised -= listener;

        public int ListenerCount => Raised?.GetInvocationList().Length ?? 0;
    }
}
