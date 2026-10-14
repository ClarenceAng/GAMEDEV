using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadeyeGulch
{
    /// <summary>
    /// The first-person body: a CharacterController with the camera at eye height. Mouse (or right stick) looks around,
    /// WASD (or left stick) shuffles along the shooting booth. Movement is kept inside a small box behind the counter and
    /// the view inside a forward-facing arc, so the player always faces the range.
    /// </summary>
    public sealed class PlayerRig : MonoBehaviour
    {
        public const float EyeHeight = 1.62f;
        const float MaxYaw = 80f, MinPitch = -35f, MaxPitch = 40f;
        const float Gravity = -20f;

        public float lookSensitivity = 0.08f;   // degrees per pixel of mouse movement
        public float padLookSpeed = 140f;       // degrees per second at full stick
        public float moveSpeed = 3.2f;

        /// <summary>When false the player can neither look nor move (title, results, pause).</summary>
        public bool InputEnabled { get; set; }

        public Camera Cam { get; private set; }
        public Transform Head { get; private set; }
        public float Yaw => yaw;
        public float Pitch => pitch;

        CharacterController cc;
        Vector3 home;
        Bounds area;
        float yaw, pitch, fallSpeed;

        public static PlayerRig Build(Camera cam, Vector3 feet, Bounds area)
        {
            var go = new GameObject("Player");
            go.layer = Shapes.NoHitLayer;
            go.transform.position = feet;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;

            var head = Shapes.Group(go.transform, "Head", new Vector3(0f, EyeHeight, 0f));
            cam.transform.SetParent(head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            var rig = go.AddComponent<PlayerRig>();
            rig.cc = cc;
            rig.Cam = cam;
            rig.Head = head;
            rig.home = feet;
            rig.area = area;
            return rig;
        }

        /// <summary>Back to the start spot, looking straight down the range.</summary>
        public void ResetPose()
        {
            cc.enabled = false;
            transform.position = home;
            cc.enabled = true;
            yaw = pitch = fallSpeed = 0f;
            Apply();
        }

        /// <summary>Turns the view by the given degrees (x = yaw to the right, y = pitch up).</summary>
        public void Look(Vector2 degrees)
        {
            yaw = Mathf.Clamp(yaw + degrees.x, -MaxYaw, MaxYaw);
            pitch = Mathf.Clamp(pitch + degrees.y, MinPitch, MaxPitch);
            Apply();
        }

        /// <summary>Points the view straight at a world position (used by tests and the attract camera).</summary>
        public void AimAt(Vector3 world)
        {
            Vector3 d = world - Head.position;
            yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -MaxYaw, MaxYaw);
            pitch = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, MinPitch, MaxPitch);
            Apply();
        }

        void Apply()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Head.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        }

        void Update()
        {
            Vector2 move = Vector2.zero;
            if (InputEnabled) move = ReadInput();

            Vector3 world = transform.rotation * new Vector3(move.x, 0f, move.y) * moveSpeed;
            fallSpeed = cc.isGrounded ? -1f : fallSpeed + Gravity * Time.deltaTime;
            world.y = fallSpeed;
            cc.Move(world * Time.deltaTime);
            KeepInArea();
        }

        Vector2 ReadInput()
        {
            // Slower turning while zoomed in, so the crosshair moves the same distance on screen.
            float zoom = Cam.fieldOfView / 60f;
            Vector2 look = Vector2.zero, move = Vector2.zero;

            var mouse = Mouse.current;
            if (mouse != null) look += mouse.delta.ReadValue() * lookSensitivity;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                look += pad.rightStick.ReadValue() * (padLookSpeed * Time.deltaTime);
                move += pad.leftStick.ReadValue();
            }

            Look(look * zoom);
            return Vector2.ClampMagnitude(move, 1f);
        }

        void KeepInArea()
        {
            Vector3 p = transform.position;
            Vector3 c = new Vector3(Mathf.Clamp(p.x, area.min.x, area.max.x), p.y, Mathf.Clamp(p.z, area.min.z, area.max.z));
            if (c == p) return;
            cc.enabled = false;
            transform.position = c;
            cc.enabled = true;
        }
    }
}
