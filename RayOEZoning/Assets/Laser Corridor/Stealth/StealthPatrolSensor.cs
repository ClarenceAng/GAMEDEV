using UnityEngine;

// V2: forward, downward, diagonal and wall-to-wall moving patrols.
// All modes use a real CheckSphere/Box/Capsule, followed by FOV + Raycast LOS.
public class StealthPatrolSensor : MonoBehaviour
{
    public enum DetectionZone { CheckSphere, CheckBox, CheckCapsule }
    public DetectionZone zone;
    public StealthV2Layout.PatrolMode patrolMode;
    public bool SeesPlayer { get; private set; }
    public string ZoneLabel => zone.ToString();
    public string PatrolLabel => patrolMode.ToString();

    private Transform player;
    private StealthFirstPersonController controller;
    private int playerMask;
    private StealthV2Layout.Guard specification;
    private int ordinal;
    private Light spotlight;
    private Material eyeMaterial;
    private Vector3 trackCenter;
    private float trackHalfDistance;
    private const int PlayerAndIntelLayers = (1 << 8) | (1 << 9);
    private const float SampleStep = .11f;

    public void Initialize(Transform target, StealthV2Layout.Guard guard, int layer,
        int index, Light sight, Renderer eye)
    {
        player = target;
        controller = target.GetComponent<StealthFirstPersonController>();
        playerMask = 1 << layer;
        zone = guard.shape;
        patrolMode = guard.mode;
        // The authored Scene position takes priority so moving the bot in the
        // Hierarchy/Scene view changes its starting patrol position.
        guard.position = transform.position;
        specification = guard;
        ordinal = index;
        spotlight = sight;
        if (eye != null) eyeMaterial = eye.material;
        FindObstacleFreeTrack();
        UpdatePose();
    }

    private void Update()
    {
        if (player == null) return;
        UpdatePose();
        // Physics movement is transform-driven and must be synced before the
        // broad-phase overlap test when Physics.autoSyncTransforms is disabled.
        Physics.SyncTransforms();
        SeesPlayer = CheckZoneAndSight();

        Color tint = SeesPlayer ? new Color(1f, .13f, .07f) : new Color(1f, .59f, .16f);
        if (spotlight != null)
        {
            spotlight.color = tint;
            spotlight.intensity = SeesPlayer ? 1400f : 750f;
        }
        if (eyeMaterial != null && eyeMaterial.HasProperty("_EmissionColor"))
            eyeMaterial.SetColor("_EmissionColor", tint * (SeesPlayer ? 5f : 2.5f));
    }

    // Instead of moving through solid boxes, pick a clear, continuous patrol
    // interval using Unity physics overlap queries BEFORE any movement begins.
    // This includes the covers, catwalk, ramps, roofs, walls and ceiling.
    // The bot never teleports across a blocked interval.
    private void FindObstacleFreeTrack()
    {
        bool alongZ = patrolMode == StealthV2Layout.PatrolMode.CrossCorridor;
        float clearance = specification.position.y >= 7.2f ? .48f : .78f;
        float bestScore = float.NegativeInfinity;
        trackCenter = specification.position;
        trackHalfDistance = 0f;

        // The original guard's z is preferred. Nearby lanes are fallback
        // when an obstacle occupies its starting point or patrol sweep.
        float[] lateralOffsets = { 0f, -.5f, .5f, -1f, 1f, -1.5f, 1.5f, -2f, 2f };
        for (int lane = 0; lane < lateralOffsets.Length; lane++)
        {
            Vector3 basePoint = specification.position;
            if (alongZ) basePoint.x = Mathf.Clamp(basePoint.x + lateralOffsets[lane], -4.15f, 4.15f);
            else basePoint.z += lateralOffsets[lane];

            float desiredAxis = alongZ ? specification.position.z : specification.position.x;
            float minAxis = alongZ ? specification.position.z - specification.travel - 1.8f
                : Mathf.Max(-4.14f, specification.position.x - specification.travel - 1.8f);
            float maxAxis = alongZ ? specification.position.z + specification.travel + 1.8f
                : Mathf.Min(4.14f, specification.position.x + specification.travel + 1.8f);
            int count = Mathf.CeilToInt((maxAxis - minAxis) / SampleStep);
            int runStart = -1;
            for (int n = 0; n <= count + 1; n++)
            {
                float axis = Mathf.Min(maxAxis, minAxis + n * SampleStep);
                Vector3 sample = basePoint;
                if (alongZ) sample.z = axis;
                else sample.x = axis;
                bool valid = n <= count &&
                    !Physics.CheckSphere(sample, clearance, ~PlayerAndIntelLayers,
                        QueryTriggerInteraction.Ignore);
                if (valid && runStart < 0) runStart = n;
                if ((!valid || n == count) && runStart >= 0)
                {
                    int runEnd = valid ? n : n - 1;
                    float freeMin = minAxis + runStart * SampleStep + .065f;
                    float freeMax = Mathf.Min(maxAxis, minAxis + runEnd * SampleStep) - .065f;
                    float half = Mathf.Max(0f, Mathf.Min(specification.travel, (freeMax - freeMin) * .5f));
                    if (freeMax >= freeMin)
                    {
                        // Choose a usable side-to-side segment near the designer's
                        // guard, not a huge empty stretch across the corridor.
                        float centerAxis = Mathf.Clamp(desiredAxis, freeMin + half, freeMax - half);
                        Vector3 center = basePoint;
                        if (alongZ) center.z = centerAxis;
                        else center.x = centerAxis;
                        float score = half * 2.25f
                            - Mathf.Abs(centerAxis - desiredAxis) * .67f
                            - Mathf.Abs(lateralOffsets[lane]) * 1.10f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            trackCenter = center;
                            trackHalfDistance = half;
                        }
                    }
                    runStart = -1;
                }
            }
        }

        if (bestScore == float.NegativeInfinity)
        {
            Debug.LogWarning("Stealth: no safe patrol track for " + specification.name +
                ". Check obstacle placement.", this);
            // No safe travel interval: prevent movement rather than clipping.
            trackHalfDistance = 0f;
        }
        else if (trackHalfDistance < .28f)
            Debug.LogWarning("Stealth: short obstacle-limited route for " + specification.name +
                " (" + trackHalfDistance.ToString("F2") + " units half-width).", this);
    }

    private void UpdatePose()
    {
        float t = Time.time * specification.speed + ordinal * 1.8f;
        Vector3 pos = trackCenter;
        Vector3 look = Vector3.back;
        switch (patrolMode)
        {
            case StealthV2Layout.PatrolMode.CeilingSweep:
                pos.x += Mathf.Sin(t) * trackHalfDistance;
                look = new Vector3(Mathf.Sin(t * .68f) * .24f, -1f,
                    -.18f + Mathf.Cos(t * .57f) * .22f).normalized;
                break;
            case StealthV2Layout.PatrolMode.DiagonalSweep:
                pos.x += Mathf.Sin(t) * trackHalfDistance;
                look = new Vector3(-.48f + Mathf.Sin(t * .63f) * .16f,
                    -.69f, -.54f).normalized;
                break;
            case StealthV2Layout.PatrolMode.CrossCorridor:
                pos.z += Mathf.Sin(t) * trackHalfDistance;
                look = new Vector3(1f, 0f, Mathf.Sin(t * .77f) * .15f).normalized;
                break;
            default:
                pos.x += Mathf.Sin(t) * trackHalfDistance;
                look = new Vector3(Mathf.Sin(t * .71f) * .48f, 0f, -1f).normalized;
                break;
        }
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(look,
            patrolMode == StealthV2Layout.PatrolMode.CeilingSweep ? Vector3.forward : Vector3.up);
    }

    private bool CheckZoneAndSight()
    {
        Vector3 origin = transform.position;
        Vector3 target = player.position + Vector3.up *
            (controller != null && controller.IsCrouching ? 1.04f : 1.55f);
        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        float range = specification.range;
        if (controller != null && controller.IsCrouching) range *= .83f;
        if (distance > range || distance <= .05f) return false;

        // This bot genuinely scans an entire width of the floor. Going ABOVE
        // the beam on the V2 catwalk bypasses it (rather than crouching below).
        if (patrolMode == StealthV2Layout.PatrolMode.CrossCorridor &&
            Mathf.Abs(target.y - origin.y) > 1.65f) return false;

        bool inside = false;
        switch (zone)
        {
            case DetectionZone.CheckSphere:
                inside = Physics.CheckSphere(origin, range, playerMask, QueryTriggerInteraction.Ignore);
                break;
            case DetectionZone.CheckBox:
                inside = Physics.CheckBox(origin + transform.forward * (range * .5f),
                    new Vector3(4.15f, patrolMode == StealthV2Layout.PatrolMode.CrossCorridor ? 2.1f : 3.5f,
                        range * .5f), transform.rotation, playerMask, QueryTriggerInteraction.Ignore);
                break;
            case DetectionZone.CheckCapsule:
                inside = Physics.CheckCapsule(origin + transform.forward * .65f,
                    origin + transform.forward * (range - .65f), 2.9f,
                    playerMask, QueryTriggerInteraction.Ignore);
                break;
        }
        if (!inside) return false;
        if (Vector3.Angle(transform.forward, delta) > specification.cone) return false;

        bool hitSomething = Physics.Raycast(origin, delta.normalized,
            out RaycastHit hit, distance + .25f,
            ~(1 << 9), QueryTriggerInteraction.Ignore);
        bool visible = hitSomething && hit.collider != null &&
            (hit.collider.transform == player || hit.collider.transform.IsChildOf(player));
        Debug.DrawRay(origin, delta.normalized * distance, visible ? Color.red : Color.cyan);
        return visible;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .67f, .12f, .9f);
        float len = specification.range > 0f ? specification.range : 10f;
        if (zone == DetectionZone.CheckSphere)
            Gizmos.DrawWireSphere(transform.position, len);
        else if (zone == DetectionZone.CheckBox)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + transform.forward * len * .5f,
                transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(8.3f, 7f, len));
            Gizmos.matrix = old;
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position + transform.forward * .65f, 2.9f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * (len - .65f), 2.9f);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * len);
    }
}
