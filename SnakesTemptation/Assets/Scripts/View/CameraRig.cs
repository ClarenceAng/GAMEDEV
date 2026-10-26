using UnityEngine;

// one camera does both looks.
// retro (Blend = 0): really high up with a tiny field of view, so it looks flat like the old snake game.
// chase (Blend = 1): behind the snake, looking down a bit, and it turns with the snake.
// in between everything just gets lerped, which looks like falling into the board
public class CameraRig
{
    public float Blend;         // 0 = retro, 1 = chase
    public float Shake;         // 0 to 1, the game sets this during the glitch
    public float FovKick;       // extra field of view that goes away by itself (for the jumpscare)
    public Vector3 FollowPos;   // where the snake head is
    public Vector3 FollowHeading = Vector3.forward;

    Camera cam;
    GameEvents events;
    float yaw;
    bool hasYaw;
    float impulse;              // shake from events
    Vector3 focus;
    bool snap;

    public CameraRig(Camera camera)
    {
        cam = camera;
    }

    public void Impulse(float amount)
    {
        impulse = Mathf.Max(impulse, amount);
    }

    // shake the camera when stuff happens
    public void Listen(GameEvents gameEvents)
    {
        if (gameEvents == null || !gameEvents.IsComplete) return;
        events = gameEvents;
        events.lanternCollected.Register(OnLanternCollected);
        events.monsterRelocated.Register(OnMonsterRelocated);
        events.monsterRose.Register(OnMonsterRose);
        events.snakeCrashed.Register(OnSnakeCrashed);
        events.snakeDevoured.Register(OnSnakeDevoured);
        events.snakeRespawned.Register(OnSnakeRespawned);
    }

    // has to be called when the game is destroyed because the event assets stay around
    public void Dispose()
    {
        if (events == null) return;
        events.lanternCollected.Unregister(OnLanternCollected);
        events.monsterRelocated.Unregister(OnMonsterRelocated);
        events.monsterRose.Unregister(OnMonsterRose);
        events.snakeCrashed.Unregister(OnSnakeCrashed);
        events.snakeDevoured.Unregister(OnSnakeDevoured);
        events.snakeRespawned.Unregister(OnSnakeRespawned);
        events = null;
    }

    void OnLanternCollected() { Impulse(0.12f); }
    void OnMonsterRelocated() { Impulse(0.45f); }
    void OnMonsterRose() { Impulse(0.9f); }
    void OnSnakeCrashed() { Impulse(1f); }
    void OnSnakeDevoured() { Impulse(2.2f); FovKick = 22f; }
    void OnSnakeRespawned() { Impulse(0.25f); }

    // jump straight to the snake instead of moving there (after a respawn)
    public void Snap()
    {
        snap = true;
        hasYaw = false;
    }

    public void Tick(float dt)
    {
        float b = Mathf.Clamp01(Blend);
        float pitch = Mathf.Lerp(90f, 32f, b);      // straight down -> 32 degrees down
        float fov = Mathf.Lerp(16f, 68f, b);
        // how much we can see goes from the whole board to just around the snake,
        // and the distance is worked out from the fov so it zooms in smoothly
        float viewSize = Mathf.Lerp(12.7f, 5.6f, b);
        float distance = viewSize / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        // turn around behind the snake when it turns
        Vector3 heading = FollowHeading;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        float targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
        if (!hasYaw || dt <= 0f)
        {
            yaw = targetYaw;
            hasYaw = true;
        }
        else
        {
            yaw = Mathf.LerpAngle(yaw, targetYaw, dt * 8f);
        }
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // look a little bit in front of the head
        Vector3 chaseFocus = FollowPos + forward * 2.2f + Vector3.up * 0.4f;
        Vector3 wantFocus = Vector3.Lerp(Vector3.zero, chaseFocus, b);
        if (snap || dt <= 0f) focus = wantFocus;
        else focus = Vector3.Lerp(focus, wantFocus, dt * 12f);
        snap = false;

        Vector3 pos = focus + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);

        float shakeAmount = Mathf.Max(Shake, impulse) * 0.18f;
        if (shakeAmount > 0f) pos += Random.insideUnitSphere * shakeAmount;
        impulse = Mathf.MoveTowards(impulse, 0f, dt * 2.2f);

        if (dt > 0f) FovKick = Mathf.Lerp(FovKick, 0f, dt * 5f);
        cam.fieldOfView = fov + FovKick;

        // when looking straight down "up" on the screen has to be forward, otherwise it's just up
        Vector3 upHint = Vector3.Lerp(Vector3.forward, Vector3.up, b * 2f);
        cam.transform.position = pos;
        cam.transform.LookAt(focus, upHint);
    }
}
