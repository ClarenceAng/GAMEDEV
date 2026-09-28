using UnityEngine;


[RequireComponent(typeof(Renderer))]
public class EdgeGuardGenerator : MonoBehaviour
{
    [Header("Wall Size")]
    [SerializeField]
    float wallHeight = 3f;

    [SerializeField]
    float wallThickness = 0.5f;

    [Header("Which Sides Get A Wall")]
    [SerializeField]
    bool guardNorth = true; // +Z

    [SerializeField]
    bool guardSouth = true; // -Z

    [SerializeField]
    bool guardEast = true; // +X

    [SerializeField]
    bool guardWest = true; // -X

    void Awake()
    {
        Bounds bounds = GetComponent<Renderer>().bounds;

        if (guardNorth)
        {
            CreateWall(
                new Vector3(bounds.center.x, bounds.max.y + wallHeight / 2f, bounds.max.z + wallThickness / 2f),
                new Vector3(bounds.size.x + wallThickness * 2f, wallHeight, wallThickness));
        }

        if (guardSouth)
        {
            CreateWall(
                new Vector3(bounds.center.x, bounds.max.y + wallHeight / 2f, bounds.min.z - wallThickness / 2f),
                new Vector3(bounds.size.x + wallThickness * 2f, wallHeight, wallThickness));
        }

        if (guardEast)
        {
            CreateWall(
                new Vector3(bounds.max.x + wallThickness / 2f, bounds.max.y + wallHeight / 2f, bounds.center.z),
                new Vector3(wallThickness, wallHeight, bounds.size.z));
        }

        if (guardWest)
        {
            CreateWall(
                new Vector3(bounds.min.x - wallThickness / 2f, bounds.max.y + wallHeight / 2f, bounds.center.z),
                new Vector3(wallThickness, wallHeight, bounds.size.z));
        }
    }

    void CreateWall(Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject("EdgeGuard");
        wall.transform.SetParent(transform, true);
        wall.transform.position = position;
        wall.layer = gameObject.layer;

        BoxCollider col = wall.AddComponent<BoxCollider>();
        col.size = size;
    }
}
