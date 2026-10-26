using UnityEngine;

public enum Speaker { Snake, Eve }

// one line of dialogue in the ending
public class DialogueLine
{
    public Speaker Who;
    public string Text;
    public string SpeakerName;

    public DialogueLine(Speaker who, string text)
    {
        Who = who;
        Text = text;
        if (who == Speaker.Snake) SpeakerName = "THE SERPENT";
        else SpeakerName = "EVE";
    }
}

// the script for the ending. if we record voice acting it has to be in this order
public static class CutsceneScript
{
    public static DialogueLine[] Lines =
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

// the timing of everything in the ending, in seconds from when it starts.
// the garden scene just looks at these numbers so it works the same at any frame rate.
// a line lasts as long as it takes to read, or as long as its voice clip if it has one
public class CutscenePlan
{
    public const int Trees = 3;
    public const float FadeIn = 1.1f;        // fade in from white
    public const float PlantDwell = 2.0f;    // how long the snake stays at each spot
    public const float LineGap = 0.3f;
    public const float FadeOut = 1.6f;       // fade to white at the end

    public float[] Arrive = { 3.2f, 7.4f, 11.4f }; // when the snake gets to each planting spot

    public float ClimbStart, ClimbEnd;      // the snake climbs the big tree
    public float EveEnter, EveArrive;       // eve walks in
    public float DialogueStart, DialogueEnd;
    public float[] LineStart, LineEnd;
    public float ReachAt, GrabAt, BiteAt;   // eve reaches, grabs the apple, bites it
    public float End;

    public CutscenePlan()
    {
        Setup(null);
    }

    // voiceSeconds = how long each line's voice clip is (0 = no voice)
    public CutscenePlan(float[] voiceSeconds)
    {
        Setup(voiceSeconds);
    }

    void Setup(float[] voiceSeconds)
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
            float voice = 0f;
            if (voiceSeconds != null && i < voiceSeconds.Length) voice = voiceSeconds[i];
            LineStart[i] = t;
            LineEnd[i] = t + LineSeconds(CutsceneScript.Lines[i], voice);
            t = LineEnd[i] + LineGap;
        }
        DialogueEnd = LineEnd[n - 1];
        ReachAt = DialogueEnd - 1.0f; // she starts reaching during the last line
        GrabAt = DialogueEnd + 0.9f;
        BiteAt = GrabAt + 1.3f;
        End = BiteAt + 1.0f + FadeOut;
    }

    public float DepartAt(int tree)
    {
        return Arrive[tree] + PlantDwell;
    }

    public float SeedAt(int tree)
    {
        return Arrive[tree] + 0.9f; // the seed comes out of the snake's mouth
    }

    public float GrowStart(int tree)
    {
        return Arrive[tree] + 1.5f;
    }

    public float GrowSeconds(int tree)
    {
        if (tree == Trees - 1) return 5.0f; // the big tree takes longer
        return 3.6f;
    }

    public float GrowEnd(int tree)
    {
        return GrowStart(tree) + GrowSeconds(tree);
    }

    // how long a line stays on screen
    public static float LineSeconds(DialogueLine line, float voiceSeconds)
    {
        if (voiceSeconds > 0.05f) return voiceSeconds + 0.4f;
        return Mathf.Clamp(0.9f + line.Text.Length * 0.05f, 2.4f, 7f);
    }

    // which line is being said at time t, -1 if none
    public int LineAt(float t)
    {
        for (int i = 0; i < LineStart.Length; i++)
        {
            if (t >= LineStart[i] && t < LineEnd[i]) return i;
        }
        return -1;
    }

    // how much of the line is typed out at time t (0 to 1)
    public float Typed(int line, float t)
    {
        if (line < 0 || line >= LineStart.Length) return 0f;
        float typeTime = Mathf.Min((LineEnd[line] - LineStart[line]) * 0.7f, 0.035f * CutsceneScript.Lines[line].Text.Length + 0.2f);
        return Mathf.Clamp01((t - LineStart[line]) / typeTime);
    }

    // the white fade, 1 = all white
    public float WhiteAt(float t)
    {
        float fadeIn = 1f - Mathf.Clamp01(t / FadeIn);
        float fadeOut = Mathf.Clamp01((t - (End - FadeOut)) / FadeOut);
        return Mathf.Max(fadeIn, Mathf.SmoothStep(0f, 1f, fadeOut));
    }
}
