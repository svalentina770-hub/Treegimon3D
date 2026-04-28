using UnityEngine;

public class BiomeZone : MonoBehaviour
{
    [SerializeField] private PlantBiomeType biomeType;
    [SerializeField] private int priority = 0;

    private Collider zoneCollider;

    public PlantBiomeType BiomeType => biomeType;
    public int Priority => priority;
    public Collider ZoneCollider => zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        BiomeZoneRegistry.Register(this);
    }

    private void OnDisable()
    {
        BiomeZoneRegistry.Unregister(this);
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        if (zoneCollider == null)
            return false;

        Vector3 closest = zoneCollider.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude < 0.0001f;
    }
}