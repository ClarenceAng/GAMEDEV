using UnityEngine;

/// A security drone that flies a patrol route and looks forward with a vision cone (the yellow/red spotlight).
/// It checks for the player in three ways:
///   1) HEARING  - Physics.CheckSphere around the drone. Walking close makes noise; sneaking shrinks the bubble.
///   2) SIGHT    - angle + distance check, then a Physics.Raycast to the player. Crates and walls block the ray.
///   3) CONTACT  - a small Physics.CheckSphere on the drone's body. Bumping into it hurts.
public class PatrolDrone : MonoBehaviour, IHazard
{
    [Header("Patrol")]
    public float speed = 2f;
    public float turnSpeedDegrees = 180f;

    [Header("Sight")]
    public float sightRange = 10f;
    public float fieldOfView = 70f;            // full cone angle
    public float sightDetectRate = 45f;        // alert points per second (more when closer)

    [Header("Hearing and contact")]
    public float hearingRadius = 2.5f;
    public float hearingDetectRate = 60f;
    public float bodyRadius = 0.5f;

    Vector3[] waypoints;
    bool loop;
    int targetIndex;
    int indexStep;

    Light visionLight;
    Material eyeMaterial;
    float alertBlend;                          // 0 = calm (yellow), 1 = sees the player (red)
    static readonly Color CalmColor = new Color(1f, 0.85f, 0.3f);
    static readonly Color AlertColor = new Color(1f, 0.1f, 0.05f);

    /// loop = true: go around the waypoints in a circle. loop = false: walk back and forth.
    public void Configure(Vector3[] path, bool loopPath, float moveSpeed)
    {
        waypoints = path;
        loop = loopPath;
        speed = moveSpeed;
        BuildBody();
        ResetHazard();
    }

    void BuildBody()
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localScale = Vector3.one * (bodyRadius * 1.4f);
        body.GetComponent<Renderer>().sharedMaterial = GameUtil.Lit(new Color(0.18f, 0.2f, 0.24f));

        eyeMaterial = GameUtil.Unlit(CalmColor);
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(eye.GetComponent<Collider>());
        eye.transform.SetParent(transform, false);
        eye.transform.localPosition = new Vector3(0f, 0f, bodyRadius * 0.6f);
        eye.transform.localScale = Vector3.one * 0.2f;
        eye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;

        // The spotlight has the same angle and range as the real vision cone, so you can see what it sees.
        visionLight = gameObject.AddComponent<Light>();
        visionLight.type = LightType.Spot;
        visionLight.spotAngle = fieldOfView;
        visionLight.range = sightRange;
        visionLight.intensity = 3f;
        visionLight.shadows = LightShadows.None;
        visionLight.color = CalmColor;
    }

    public void ResetHazard()
    {
        transform.position = waypoints[0];
        targetIndex = 1;
        indexStep = 1;
        transform.rotation = Quaternion.LookRotation((waypoints[1] - waypoints[0]).normalized);
        alertBlend = 0f;
    }

    void Update()
    {
        Patrol();

        bool seesPlayer = false;
        StealthCorridorGame game = StealthCorridorGame.Instance;
        if (game != null && game.IsPlaying)
        {
            CheckHearing(game);
            seesPlayer = CheckSight(game);
            CheckContact(game);
        }
        UpdateAlertColor(seesPlayer);
    }

    // ------------------------------------------------------------ movement
    void Patrol()
    {
        Vector3 toTarget = waypoints[targetIndex] - transform.position;
        float distance = toTarget.magnitude;
        if (distance < 0.1f)
        {
            ChooseNextWaypoint();
            return;
        }

        Vector3 direction = toTarget / distance;
        transform.position += direction * Mathf.Min(speed * Time.deltaTime, distance);

        Quaternion wanted = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeedDegrees * Time.deltaTime);
    }

    void ChooseNextWaypoint()
    {
        if (loop)
        {
            targetIndex = (targetIndex + 1) % waypoints.Length;
            return;
        }

        bool wouldLeaveList = targetIndex + indexStep < 0 || targetIndex + indexStep >= waypoints.Length;
        if (wouldLeaveList) indexStep = -indexStep;
        targetIndex += indexStep;
    }

    // ------------------------------------------------------------ detection
    /// 1) Hearing: is the player inside the noise bubble?
    void CheckHearing(StealthCorridorGame game)
    {
        float radius = hearingRadius * game.NoiseMultiplier;
        if (Physics.CheckSphere(transform.position, radius, GameLayers.PlayerMask))
            game.ReportSpotted(hearingDetectRate);
    }

    /// 2) Sight: inside range, inside the cone, and nothing in the way. Returns true if the drone sees the player.
    bool CheckSight(StealthCorridorGame game)
    {
        float range = sightRange * game.LightMultiplier;       // a lit-up player is seen from further away
        Vector3 eye = transform.position;
        Vector3 toPlayer = game.PlayerChest - eye;
        float distance = toPlayer.magnitude;

        if (distance > range) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > fieldOfView * 0.5f) return false;

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(eye, toPlayer / distance, out hit, distance + 0.5f, GameLayers.SightMask, QueryTriggerInteraction.Ignore);
        if (!hitSomething || hit.collider.gameObject.layer != GameLayers.Player) return false;   // a wall or crate got hit first

        float closeness = 1f - distance / range;                           // 0 far away, 1 point-blank
        float rate = sightDetectRate * Mathf.Lerp(0.5f, 1.5f, closeness) * game.LightMultiplier;
        game.ReportSpotted(rate);
        return true;
    }

    /// 3) Contact: touching the drone's body.
    void CheckContact(StealthCorridorGame game)
    {
        if (Physics.CheckSphere(transform.position, bodyRadius, GameLayers.PlayerMask))
            game.PlayerTouchedDrone();
    }

    void UpdateAlertColor(bool seesPlayer)
    {
        alertBlend = Mathf.MoveTowards(alertBlend, seesPlayer ? 1f : 0f, Time.deltaTime * 6f);
        Color color = Color.Lerp(CalmColor, AlertColor, alertBlend);
        visionLight.color = color;
        eyeMaterial.color = color;
    }
}
