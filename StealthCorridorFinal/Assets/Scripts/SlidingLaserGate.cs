using UnityEngine;

/// The original laser wall with a gap in it, but now it slides back and forth along the corridor.
/// Line up with the gap and move with it.
///   Physics.CheckBox - one check per beam segment (the left piece and the right piece of the wall).
public class SlidingLaserGate : MonoBehaviour, IHazard
{
    public float nearZ = 3f;
    public float farZ = 9f;
    public float speed = 2.5f;
    public float thickness = 0.25f;

    Transform[] segments;
    float timer;

    public void Configure(float corridorWidth, float corridorHeight, float gapWidth, float gapCenterX, float zA, float zB, float slideSpeed)
    {
        nearZ = zA;
        farZ = zB;
        speed = slideSpeed;

        float leftEdge = -corridorWidth / 2f;
        float rightEdge = corridorWidth / 2f;
        float gapLeft = gapCenterX - gapWidth / 2f;
        float gapRight = gapCenterX + gapWidth / 2f;

        Material red = GameUtil.Unlit(new Color(1f, 0.05f, 0.05f));
        Transform left = MakeSegment(leftEdge, gapLeft, corridorHeight, red);
        Transform right = MakeSegment(gapRight, rightEdge, corridorHeight, red);
        segments = new Transform[] { left, right };
        ResetHazard();
    }

    Transform MakeSegment(float fromX, float toX, float height, Material material)
    {
        float width = toX - fromX;
        if (width < 0.01f) return null;
        Vector3 center = new Vector3((fromX + toX) / 2f, height / 2f, 0f);
        return GameUtil.Box(transform, center, new Vector3(width, height, thickness), material, false).transform;
    }

    public void ResetHazard()
    {
        timer = 0f;
        MoveGate();
    }

    void Update()
    {
        timer += Time.deltaTime;
        MoveGate();

        StealthCorridorGame game = StealthCorridorGame.Instance;
        if (game == null || !game.IsPlaying) return;

        foreach (Transform segment in segments)
        {
            if (segment == null) continue;
            Vector3 halfSize = segment.localScale * 0.5f;
            if (Physics.CheckBox(segment.position, halfSize, segment.rotation, GameLayers.PlayerMask))
            {
                game.PlayerTouchedLaser();
                return;
            }
        }
    }

    void MoveGate()
    {
        float travel = Mathf.Max(0.01f, Mathf.Abs(farZ - nearZ));
        float t = Mathf.PingPong(timer * speed / travel, 1f);
        transform.position = new Vector3(0f, 0f, Mathf.Lerp(nearZ, farZ, t));
    }
}
