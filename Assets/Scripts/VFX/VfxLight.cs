using UnityEngine;


public class VfxLight : MonoBehaviour
{
    public Light lightRef;
    public AnimationCurve curve = AnimationCurve.Linear(0, 1, 1, 1);
    public float peak = 5f, duration = 1f, flicker = 0f;
    public bool loop;
    float t, seed;

    public static VfxLight Spawn(Transform parent, Vector3 localPos, Color color, float peak, float range, float duration,
        AnimationCurve curve = null, bool loop = false, float flicker = 0f)
    {
        var go = new GameObject("VfxLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.range = range; l.intensity = 0f; l.shadows = LightShadows.None;
        var v = go.AddComponent<VfxLight>();
        v.lightRef = l; v.peak = peak; v.duration = duration; v.loop = loop; v.flicker = flicker;
        if (curve != null) v.curve = curve;
        v.seed = Random.value * 100f;
        return v;
    }

    void Update()
    {
        t += Time.deltaTime;
        float n = duration > 0f ? t / duration : 0f;
        if (loop) n %= 1f;
        else if (n > 1f) { lightRef.intensity = 0f; return; }
        float k = curve.Evaluate(n);
        if (flicker > 0f) k *= 1f - flicker * Mathf.PerlinNoise(Time.time * 18f, seed);
        lightRef.intensity = peak * k;
    }
}
