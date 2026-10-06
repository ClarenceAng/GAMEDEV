using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Distance")]
    public float distance = 6f;
    public float minimumDistance = 0f;
    public float maximumDistance = 12f;
    public float zoomSpeed = 10f;

    [Header("Camera Rotation")]
    public float mouseSensitivity = 1f;
    public float cameraSensitivityMultiplier = 5f;
    public float verticalMin = -80f;
    public float verticalMax = 80f;

    [Header("First Person")]
    public Renderer[] playerRenderers;

    private float horizontalRotation = 0f;
    private float verticalRotation = 20f;

    private bool firstPerson = false;

    void LateUpdate()
    {
        if (target == null)
            return;

        HandleZoom();
        HandleRotation();
        UpdateCameraPosition();
        UpdateFirstPerson();
    }

    void HandleRotation()
    {
        if (Mouse.current == null)
            return;

        bool shouldRotate = false;

        // Third-person camera.
        // Hold right-click to rotate.
        if (!firstPerson &&
            Mouse.current.rightButton.isPressed)
        {
            shouldRotate = true;
        }

        // First-person camera.
        // Move the mouse freely to look around.
        if (firstPerson)
        {
            shouldRotate = true;
        }

        if (!shouldRotate)
            return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        float sensitivity =
            mouseSensitivity * cameraSensitivityMultiplier;

        horizontalRotation +=
            mouseDelta.x * sensitivity;

        verticalRotation -=
            mouseDelta.y * sensitivity;

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            verticalMin,
            verticalMax
        );
    }

    void HandleZoom()
    {
        if (Mouse.current == null)
            return;

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        distance -=
            scroll * zoomSpeed * 0.1f;

        distance = Mathf.Clamp(
            distance,
            minimumDistance,
            maximumDistance
        );
    }

    void UpdateCameraPosition()
    {
        Quaternion rotation =
            Quaternion.Euler(
                verticalRotation,
                horizontalRotation,
                0f
            );

        Vector3 offset =
            rotation *
            new Vector3(
                0f,
                0f,
                -distance
            );

        transform.position =
            target.position + offset;

        transform.rotation = rotation;
    }

    void UpdateFirstPerson()
    {
        bool shouldBeFirstPerson =
            distance <= minimumDistance + 0.05f;

        if (shouldBeFirstPerson != firstPerson)
        {
            firstPerson = shouldBeFirstPerson;

            SetPlayerVisibility(!firstPerson);
        }
    }

    void SetPlayerVisibility(bool visible)
    {
        if (playerRenderers == null)
            return;

        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }
}