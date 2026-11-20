using UnityEngine;

// Camera shortcuts for the three effects.
public sealed class ShowcaseCameraCuts : MonoBehaviour
{
    public Camera showcaseCamera;
    public Vector3[] stations = new Vector3[3];
    public Vector3 overviewPosition;
    public Quaternion overviewRotation;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0)) ViewAll();
        if (Input.GetKeyDown(KeyCode.Alpha1)) ViewStation(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ViewStation(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ViewStation(2);
        if (Input.GetKeyDown(KeyCode.R)) Replay();
    }

    void ViewAll()
    {
        if (!showcaseCamera) return;
        showcaseCamera.transform.SetPositionAndRotation(overviewPosition, overviewRotation);
    }

    void ViewStation(int index)
    {
        if (!showcaseCamera || stations == null || index >= stations.Length) return;
        Vector3 focalPoint = stations[index] + Vector3.up * .4f;
        Vector3 cameraPosition = focalPoint + new Vector3(0, 1.6f, -6.8f);
        showcaseCamera.transform.position = cameraPosition;
        showcaseCamera.transform.LookAt(focalPoint);
    }

    void Replay()
    {
        foreach (var system in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (system.subEmitters.enabled && system.transform.parent && system.transform.parent.GetComponent<ParticleSystem>())
                continue;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(true);
        }
    }
}
