using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BiomeZone : MonoBehaviour
{
    [Header("Configuración de bioma")]
    [SerializeField] private PlantBiomeType biomeType;
    [SerializeField] private int priority = 0;

    [Header("Configuración de detección")]
    [Tooltip("Si está activo, fuerza que el Collider de esta zona sea Trigger. Recomendado para zonas lógicas invisibles.")]
    [SerializeField] private bool forceTrigger = true;

    [Tooltip("Margen usado al evaluar si un punto está dentro del Collider. Ayuda a evitar errores por precisión flotante.")]
    [SerializeField] private float containsTolerance = 0.01f;

    [Header("Debug visual")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(0f, 0.8f, 1f, 0.18f);

    private Collider zoneCollider;

    public PlantBiomeType BiomeType => biomeType;
    public int Priority => priority;
    public Collider ZoneCollider => zoneCollider;
    public bool ForceTrigger => forceTrigger;

    private void Awake()
    {
        CacheCollider();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        CacheCollider();
        ConfigureCollider();
    }

    private void OnEnable()
    {
        CacheCollider();
        ConfigureCollider();
        BiomeZoneRegistry.Register(this);
    }

    private void OnDisable()
    {
        BiomeZoneRegistry.Unregister(this);
    }

    private void CacheCollider()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider>();
    }

    private void ConfigureCollider()
    {
        if (zoneCollider == null)
            return;

        if (forceTrigger)
            zoneCollider.isTrigger = true;
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        if (zoneCollider == null)
            return false;

        Vector3 closest = zoneCollider.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude <= containsTolerance * containsTolerance;
    }

    public Vector3 GetClosestPoint(Vector3 worldPoint)
    {
        if (zoneCollider == null)
            return transform.position;

        return zoneCollider.ClosestPoint(worldPoint);
    }

    public Vector3 GetZoneCenter()
    {
        if (zoneCollider == null)
            return transform.position;

        return zoneCollider.bounds.center;
    }

    public Vector3 GetRandomPointInsideZone(float yOffset = 0f)
    {
        if (zoneCollider == null)
            return transform.position;

        if (zoneCollider is SphereCollider sphereCollider)
            return GetRandomPointInsideSphereCollider(sphereCollider, yOffset);

        Bounds bounds = zoneCollider.bounds;

        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPoint = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y + yOffset,
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (ContainsPoint(randomPoint))
                return randomPoint;
        }

        return GetZoneCenter();
    }

    private Vector3 GetRandomPointInsideSphereCollider(SphereCollider sphereCollider, float yOffset)
    {
        Vector2 randomCircle = Random.insideUnitCircle * sphereCollider.radius;
        Vector3 localPoint = sphereCollider.center + new Vector3(randomCircle.x, yOffset, randomCircle.y);
        return sphereCollider.transform.TransformPoint(localPoint);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        CacheCollider();

        if (zoneCollider == null)
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = gizmoColor;

        if (zoneCollider is SphereCollider sphereCollider)
        {
            DrawSphereZoneGizmo(sphereCollider);
            Gizmos.color = previousColor;
            return;
        }

        Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.65f);
        Gizmos.DrawWireCube(zoneCollider.bounds.center, zoneCollider.bounds.size);

        Gizmos.color = previousColor;
    }

    private void DrawSphereZoneGizmo(SphereCollider sphereCollider)
    {
        Vector3 worldCenter = sphereCollider.transform.TransformPoint(sphereCollider.center);
        float maxScale = Mathf.Max(
            Mathf.Abs(sphereCollider.transform.lossyScale.x),
            Mathf.Abs(sphereCollider.transform.lossyScale.y),
            Mathf.Abs(sphereCollider.transform.lossyScale.z)
        );

        float worldRadius = sphereCollider.radius * maxScale;

        Gizmos.DrawSphere(worldCenter, worldRadius);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.65f);
        Gizmos.DrawWireSphere(worldCenter, worldRadius);
    }
}