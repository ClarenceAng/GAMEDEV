using NUnit.Framework;
using SnakesTemptation;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    public class CutscenePlanTests
    {
        [Test]
        public void TheScript_HasAConversation_BetweenTheSerpentAndEve()
        {
            var lines = CutsceneScript.Lines;
            Assert.GreaterOrEqual(lines.Length, 7);
            Assert.AreEqual(Speaker.Snake, lines[0].Who, "the serpent speaks first");
            bool snake = false, eve = false;
            foreach (var l in lines)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(l.Text));
                if (l.Who == Speaker.Snake) snake = true; else eve = true;
            }
            Assert.IsTrue(snake && eve);
            Assert.AreEqual("THE SERPENT", lines[0].SpeakerName);
            Assert.AreEqual("EVE", lines[1].SpeakerName);
        }

        [Test]
        public void TheTimeline_RunsInOrder_AndIsAShortScene()
        {
            var p = new CutscenePlan();
            for (int i = 0; i < CutscenePlan.Trees; i++)
            {
                Assert.Less(p.Arrive[i], p.SeedAt(i));
                Assert.Less(p.SeedAt(i), p.GrowStart(i));
                Assert.Less(p.GrowStart(i), p.GrowEnd(i));
                Assert.Less(p.SeedAt(i) + 0.5f, p.DepartAt(i), "the seed is in the ground before the snake leaves");
                if (i > 0) Assert.Less(p.DepartAt(i - 1), p.Arrive[i], "it plants one at a time");
            }
            Assert.Less(p.GrowEnd(2), p.ClimbEnd);
            Assert.Less(p.EveEnter, p.EveArrive);
            Assert.LessOrEqual(p.EveArrive, p.DialogueStart);
            Assert.Less(p.ClimbEnd, p.DialogueStart, "the snake is in place before it speaks");
            for (int i = 0; i < p.LineStart.Length; i++)
            {
                Assert.Less(p.LineStart[i], p.LineEnd[i]);
                if (i > 0) Assert.Less(p.LineEnd[i - 1], p.LineStart[i], "lines do not overlap");
            }
            Assert.AreEqual(p.DialogueStart, p.LineStart[0], 0.0001f);
            Assert.Less(p.ReachAt, p.GrabAt);
            Assert.Less(p.GrabAt, p.BiteAt);
            Assert.Greater(p.End, p.BiteAt);
            Assert.That(p.End, Is.InRange(30f, 95f), "a small scene, not a film");
        }

        [Test]
        public void ALineIsOnScreenLongEnoughToRead([Values(0, 1, 2, 3, 4, 5, 6, 7, 8)] int i)
        {
            var p = new CutscenePlan();
            float secs = p.LineEnd[i] - p.LineStart[i];
            float readable = CutsceneScript.Lines[i].Text.Length / 22f; // ~22 characters a second is a fast reader
            Assert.GreaterOrEqual(secs, readable, "line " + i + " is too short to read");
            Assert.GreaterOrEqual(secs, 2.4f);
        }

        [Test]
        public void AVoiceClip_SetsTheLengthOfItsLine()
        {
            var plain = new CutscenePlan();
            var voice = new float[CutsceneScript.Lines.Length];
            voice[3] = 9f;
            var voiced = new CutscenePlan(voice);
            Assert.AreEqual(9.4f, voiced.LineEnd[3] - voiced.LineStart[3], 0.001f, "the clip plus a breath");
            Assert.AreEqual(plain.LineEnd[0] - plain.LineStart[0], voiced.LineEnd[0] - voiced.LineStart[0], 0.001f, "unvoiced lines are unchanged");
            float extra = 9.4f - (plain.LineEnd[3] - plain.LineStart[3]);
            Assert.AreEqual(plain.End + extra, voiced.End, 0.01f, "everything after it moves later");
            Assert.AreEqual(plain.DialogueStart, voiced.DialogueStart, 0.001f, "...and everything before it stays");
        }

        [Test]
        public void ShortOrMissingVoiceData_IsIgnored()
        {
            var a = new CutscenePlan();
            var b = new CutscenePlan(new float[] { 0f, 0f });
            var c = new CutscenePlan(new float[0]);
            Assert.AreEqual(a.End, b.End, 0.001f);
            Assert.AreEqual(a.End, c.End, 0.001f);
        }

        [Test]
        public void LineAt_FindsTheLineBeingSpoken()
        {
            var p = new CutscenePlan();
            Assert.AreEqual(-1, p.LineAt(0f));
            Assert.AreEqual(-1, p.LineAt(p.LineStart[0] - 0.01f));
            for (int i = 0; i < p.LineStart.Length; i++)
            {
                Assert.AreEqual(i, p.LineAt(p.LineStart[i]));
                Assert.AreEqual(i, p.LineAt((p.LineStart[i] + p.LineEnd[i]) * 0.5f));
                Assert.AreEqual(-1, p.LineAt(p.LineEnd[i] + 0.01f), "a gap after each line");
            }
            Assert.AreEqual(-1, p.LineAt(p.End));
        }

        [Test]
        public void TheWordsAreTypedOut_AndAreAllThereWellBeforeTheLineEnds()
        {
            var p = new CutscenePlan();
            for (int i = 0; i < p.LineStart.Length; i++)
            {
                Assert.AreEqual(0f, p.Typed(i, p.LineStart[i] - 1f));
                Assert.AreEqual(0f, p.Typed(i, p.LineStart[i]), 0.001f);
                float mid = p.Typed(i, p.LineStart[i] + 0.4f);
                Assert.That(mid, Is.InRange(0.001f, 1f));
                Assert.AreEqual(1f, p.Typed(i, p.LineEnd[i] - 0.4f), "fully typed before it goes away");
            }
            Assert.AreEqual(0f, p.Typed(-1, 5f));
        }

        [Test]
        public void TheFade_StartsWhiteAndEndsWhite_WithTheGardenInBetween()
        {
            var p = new CutscenePlan();
            Assert.AreEqual(1f, p.WhiteAt(0f), 0.001f);
            Assert.AreEqual(0f, p.WhiteAt(CutscenePlan.FadeIn), 0.001f);
            Assert.AreEqual(0f, p.WhiteAt(p.End * 0.5f), 0.001f);
            Assert.AreEqual(0f, p.WhiteAt(p.BiteAt), 0.001f, "the bite is seen clearly");
            Assert.AreEqual(1f, p.WhiteAt(p.End), 0.001f);
            float prev = 0f;
            for (float t = p.End - CutscenePlan.FadeOut; t <= p.End; t += 0.1f)
            {
                float w = p.WhiteAt(t);
                Assert.GreaterOrEqual(w, prev - 0.0001f, "the fade out only gets whiter");
                prev = w;
            }
        }
    }
}
