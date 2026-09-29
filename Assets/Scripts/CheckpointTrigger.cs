using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField]
    GameObject obj;

    [SerializeField]
    List<GameObject> allPlatforms = new();

    Vector3 pos;

    void Start()
    {
        pos = obj.transform.position;
        if (File.Exists(Application.persistentDataPath + "/rotationdata.json"))
        {
            var data = JsonUtility.FromJson<Vector3>(Application.persistentDataPath + "/rotationdata.json");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            obj.transform.Rotate(Vector3.up, 45);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        string data = JsonUtility.ToJson(obj.transform.rotation);
        File.WriteAllText(Application.persistentDataPath + "/rotationdata.json", data);
    }

    private void OnTriggerStay(Collider other)
    {

    }
}
