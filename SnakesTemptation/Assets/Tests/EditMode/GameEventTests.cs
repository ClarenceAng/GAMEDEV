using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    public class GameEventTests
    {
        [Test]
        public void Raise_CallsEveryListener_AndUnregisterStopsIt()
        {
            var channel = ScriptableObject.CreateInstance<GameEvent>();
            int a = 0, b = 0;
            System.Action listenA = () => a++, listenB = () => b++;
            channel.Register(listenA);
            channel.Register(listenB);
            channel.Raise();
            Assert.AreEqual((1, 1), (a, b));
            Assert.AreEqual(2, channel.ListenerCount);

            channel.Unregister(listenA);
            channel.Raise();
            Assert.AreEqual((1, 2), (a, b), "an unregistered listener hears nothing more");
            channel.Unregister(listenB);
            Assert.AreEqual(0, channel.ListenerCount);
            channel.Raise(); // with nobody listening this is a no-op
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void TheProjectAssets_HaveEveryChannelAndEverySound()
        {
            var events = AssetDatabase.LoadAssetAtPath<GameEvents>("Assets/ScriptableObjects/GameEvents.asset");
            Assert.IsNotNull(events);
            Assert.IsTrue(events.IsComplete, "every gameplay moment has a channel");
            var distinct = new HashSet<GameEvent>(events.All);
            Assert.AreEqual(events.All.Length, distinct.Count, "each moment has its own channel");

            var sounds = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/ScriptableObjects/SoundSet.asset");
            Assert.IsNotNull(sounds);
            Assert.IsTrue(sounds.IsComplete, "every sound points at a WAV file");
        }
    }
}
