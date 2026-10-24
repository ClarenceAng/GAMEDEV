using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    public enum Speaker { Snake, Eve }

    /// <summary>One spoken line of the ending. Voice acting is optional and added later: see SnakeGameController.cutsceneVoice.</summary>
    public sealed class DialogueLine
    {
        public readonly Speaker Who;
        public readonly string Text;

        public DialogueLine(Speaker who, string text)
        {
            Who = who;
            Text = text;
        }

        public string SpeakerName => Who == Speaker.Snake ? "THE SERPENT" : "EVE";
    }

    /// <summary>The words of the ending. To record voice acting, record these lines in this order and drop the clips into the controller.</summary>
    public static class CutsceneScript
    {
        public static readonly DialogueLine[] Lines =
        {
            new DialogueLine(Speaker.Snake, "Sssso... you wander the garden alone, Eve."),
            new DialogueLine(Speaker.Eve,   "Who speaks? ...Oh! A serpent. I did not know you could talk."),
            new DialogueLine(Speaker.Snake, "There is much you do not know. Look: this tree was not here a moment ago."),
            new DialogueLine(Speaker.Eve,   "It is beautiful. But we were told never to eat its fruit."),
            new DialogueLine(Speaker.Snake, "Told? Why would anyone hide something so sweet from you?"),
            new DialogueLine(Speaker.Eve,   "We were told that we would die."),
            new DialogueLine(Speaker.Snake, "Die? Sssurely not. Your eyes will be opened, and you will see everything. Just one bite, Eve."),
            new DialogueLine(Speaker.Eve,   "...Just one?"),
            new DialogueLine(Speaker.Snake, "Just one. And it will be our little ssecret."),
        };
    }

    /// <summary>
    /// When everything in the ending happens, in seconds from its start. Pure data: the garden scene is a function of this
    /// timeline (and the tests read it), so nothing here depends on frame rate. Lines normally last as long as they take to
    /// read; if a voice clip is supplied for a line, the line lasts as long as the clip.
    /// </summary>
    public sealed class CutscenePlan
    {
        public const int Trees = 3;
        public const float FadeIn = 1.1f;        // from white (the last seed's flash) into the garden
        public const float PlantDwell = 2.0f;    // how long the snake stays at each planting spot
        public const float LineGap = 0.3f;
        public const float FadeOut = 1.6f;       // to white after the bite

        /// <summary>When the snake reaches each planting spot.</summary>
        public readonly float[] Arrive = { 3.2f, 7.4f, 11.4f };

        public float DepartAt(int tree) => Arrive[tree] + PlantDwell;
        public float SeedAt(int tree) => Arrive[tree] + 0.9f;        // the seed leaves the snake's mouth
        public float GrowStart(int tree) => Arrive[tree] + 1.5f;
        public float GrowSeconds(int tree) => tree == Trees - 1 ? 5.0f : 3.6f;
        public float GrowEnd(int tree) => GrowStart(tree) + GrowSeconds(tree);

        public readonly float ClimbStart, ClimbEnd;     // the snake winds up the big tree
        public readonly float EveEnter, EveArrive;      // Eve walks in
        public readonly float DialogueStart, DialogueEnd;
        public readonly float[] LineStart, LineEnd;
        public readonly float ReachAt, GrabAt, BiteAt;  // her hand goes out, closes on the apple, and she bites
        public readonly float End;                      // the cutscene is over (the screen is white)

        /// <param name="voiceSeconds">Per line: the length of its voice clip, or 0 / missing for none.</param>
        public CutscenePlan(IList<float> voiceSeconds = null)
        {
            ClimbStart = GrowEnd(Trees - 1) - 1.3f;
            ClimbEnd = ClimbStart + 5.0f;
            EveEnter = ClimbStart - 1.4f;
            EveArrive = ClimbEnd + 0.2f;
            DialogueStart = EveArrive + 0.6f;

            int n = CutsceneScript.Lines.Length;
            LineStart = new float[n];
            LineEnd = new float[n];
            float t = DialogueStart;
            for (int i = 0; i < n; i++)
            {
                float voice = voiceSeconds != null && i < voiceSeconds.Count ? voiceSeconds[i] : 0f;
                LineStart[i] = t;
                LineEnd[i] = t + LineSeconds(CutsceneScript.Lines[i], voice);
                t = LineEnd[i] + LineGap;
            }
            DialogueEnd = LineEnd[n - 1];
            ReachAt = DialogueEnd - 1.0f;   // she is already reaching as the last words are said
            GrabAt = DialogueEnd + 0.9f;
            BiteAt = GrabAt + 1.3f;
            End = BiteAt + 1.0f + FadeOut;
        }

        /// <summary>How long a line is on screen: its voice clip (plus a breath) or the time it takes to read it.</summary>
        public static float LineSeconds(DialogueLine line, float voiceSeconds)
        {
            if (voiceSeconds > 0.05f) return voiceSeconds + 0.4f;
            return Mathf.Clamp(0.9f + line.Text.Length * 0.05f, 2.4f, 7f);
        }

        /// <summary>The line being spoken at time t, or -1 between lines.</summary>
        public int LineAt(float t)
        {
            for (int i = 0; i < LineStart.Length; i++)
                if (t >= LineStart[i] && t < LineEnd[i]) return i;
            return -1;
        }

        /// <summary>How much of the line has been typed out by time t (0..1). It types over the first part of the line.</summary>
        public float Typed(int line, float t)
        {
            if (line < 0 || line >= LineStart.Length) return 0f;
            float span = Mathf.Min((LineEnd[line] - LineStart[line]) * 0.7f, 0.035f * CutsceneScript.Lines[line].Text.Length + 0.2f);
            return Mathf.Clamp01((t - LineStart[line]) / Mathf.Max(0.01f, span));
        }

        /// <summary>The white fade over the whole ending: in at the start, out at the end. 1 = pure white.</summary>
        public float WhiteAt(float t)
        {
            float inA = 1f - Mathf.Clamp01(t / FadeIn);
            float outA = Mathf.Clamp01((t - (End - FadeOut)) / FadeOut);
            return Mathf.Max(inA, MathX.Smooth01(outA));
        }
    }
}
