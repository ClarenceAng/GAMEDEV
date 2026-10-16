using UnityEngine;
using UnityEngine.Rendering;

/// Layer numbers used by the game.
/// Layers 8 and up work by number even if they have no name in Project Settings > Tags and Layers,
/// so you do not need to set anything up. (Layer 0 = Default = walls, floor, crates.)
public static class GameLayers
{
    public const int Player = 8;
    public const int Hidden = 9;

    public const int PlayerMask = 1 << Player;
    public const int HiddenMask = 1 << Hidden;
    public const int WorldMask = 1 << 0;                    // things that block sight and light
    public const int SightMask = WorldMask | PlayerMask;    // what a drone's sight ray can hit
}

/// Anything that moves around and can hurt or spot the player. The game resets them after a death.
public interface IHazard
{
    void ResetHazard();
}

/// Small helpers for building the level out of cubes and flat-coloured materials.
public static class GameUtil
{
    /// Flat colour that ignores lights. Used for lasers, the exit door and floor panels (they glow in the dark).
    public static Material Unlit(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    /// Normal lit material, so the flashlight and drone lights actually light up walls and objects.
    /// Picks the URP shader if the project uses URP, otherwise the Built-in "Standard" shader.
    public static Material Lit(Color color, float emissionStrength = 0f)
    {
        Shader shader = GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return Unlit(color);

        Material material = new Material(shader);
        material.color = color;
        if (emissionStrength > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionStrength);
        }
        return material;
    }

    /// Makes a cube. 'solid' = keep its collider (walls, floor, crates). Lasers and decorations have no collider.
    public static GameObject Box(Transform parent, Vector3 localPosition, Vector3 size, Material material, bool solid)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!solid) Object.DestroyImmediate(cube.GetComponent<Collider>());   // immediate, so rays never see it
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }
}
