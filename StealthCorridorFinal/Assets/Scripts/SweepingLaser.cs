using UnityEngine;

/// A laser emitter on the corridor wall. The beam swings left and right like a radar arm.
///   Physics.Raycast       - finds where the beam ends (the opposite wall) so it never pokes through walls.
///   Physics.CheckCapsule  - a thin capsule along the beam; if it overlaps the player, you got zapped.
/// The beam is low (knee height). Jump over it with Space.
public class SweepingLaser : MonoBehaviour, IHazard
{
    public float baseYawDegrees = 0f;      // 0 = points across the corridor along +X (use 180 for the right wall)
    public float sweepArcDegrees = 55f;    // how far it swings to each side
    public float sweepSpeed = 0.8f;
    public float phaseSeconds = 0f;
    public float beamRadius = 0.06f;
    public float maxBeamLength = 14f;

    Transform beam;
    float timer;

    public void Configure(Vector3 emitterPosition, float baseYaw, float sweepArc, float speed, float phase)
    {
        transform.position = emitterPosition;
        baseYawDegrees = baseYaw;
        sweepArcDegrees = sweepArc;
        sweepSpeed = speed;
        phaseSeconds = phase;

        Material red = GameUtil.Unlit(new Color(1f, 0.05f, 0.05f));
        GameUtil.Box(transform, Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), GameUtil.Lit(new Color(0.15f, 0.15f, 0.17f)), false);
        beam = GameUtil.Box(transform, Vector3.zero, Vector3.one, red, false).transform;
        ResetHazard();
    }

    public void ResetHazard()
    {
        timer = 0f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        float yaw = baseYawDegrees + Mathf.Sin((timer + phaseSeconds) * sweepSpeed) * sweepArcDegrees;
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 origin = transform.position;

        // Raycast: stop the beam at the first wall / crate
        float length = maxBeamLength;
        RaycastHit wallHit;
        if (Physics.Raycast(origin, direction, out wallHit, maxBeamLength, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
            length = wallHit.distance;

        // Draw the beam: a long thin cube from the emitter to where the ray ended
        beam.position = origin + direction * (length * 0.5f);
        beam.rotation = Quaternion.LookRotation(direction);
        beam.localScale = new Vector3(beamRadius * 2f, beamRadius * 2f, length);

        // CheckCapsule: is the player touching the beam?
        StealthCorridorGame game = StealthCorridorGame.Instance;
        if (game == null || !game.IsPlaying) return;
        Vector3 end = origin + direction * length;
        if (Physics.CheckCapsule(origin, end, beamRadius * 1.5f, GameLayers.PlayerMask))
            game.PlayerTouchedLaser();
    }
}
