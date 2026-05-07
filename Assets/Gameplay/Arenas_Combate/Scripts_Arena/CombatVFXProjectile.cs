using UnityEngine;

public class CombatVFXProjectile : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float arriveDistance = 0.25f;
    [SerializeField] private float maxLifetime = 5f;

    [Header("Impacto")]
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private bool destroyOnImpact = true;

    private Transform target;
    private Vector3 lastKnownTargetPosition;
    private Quaternion lastKnownImpactRotation = Quaternion.identity;
    private float lifeTimer;
    private bool initialized;

    public void Initialize(Transform targetPoint, GameObject impactEffectPrefab = null, float speedOverride = -1f, float lifetimeOverride = -1f)
    {
        target = targetPoint;

        if (target != null)
        {
            lastKnownTargetPosition = target.position;

            Vector3 initialDirection = lastKnownTargetPosition - transform.position;
            if (initialDirection.sqrMagnitude > 0.001f)
                lastKnownImpactRotation = Quaternion.LookRotation(initialDirection.normalized, Vector3.up);
        }
        else
        {
            lastKnownTargetPosition = transform.position;
            lastKnownImpactRotation = transform.rotation;
        }

        if (impactEffectPrefab != null)
            impactPrefab = impactEffectPrefab;

        if (speedOverride > 0f)
            moveSpeed = speedOverride;

        if (lifetimeOverride > 0f)
            maxLifetime = lifetimeOverride;

        initialized = true;
        lifeTimer = 0f;
    }

    private void Update()
    {
        if (!initialized || target == null)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= maxLifetime)
                Destroy(gameObject);

            return;
        }

        lifeTimer += Time.deltaTime;

        lastKnownTargetPosition = target.position;
        Vector3 direction = lastKnownTargetPosition - transform.position;

        if (direction.sqrMagnitude > 0.001f)
            lastKnownImpactRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (direction.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            ImpactAt(lastKnownTargetPosition, lastKnownImpactRotation);
            return;
        }

        transform.position += direction.normalized * moveSpeed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.001f)
            transform.forward = direction.normalized;

        if (lifeTimer >= maxLifetime)
            ImpactAt(lastKnownTargetPosition, lastKnownImpactRotation);
    }

    private void OnTriggerEnter(Collider other)
    {
        // No usamos colisión física para resolver el impacto del VFX.
        // En la arena el proyectil puede nacer muy cerca o dentro del trigger del Player/Boss,
        // haciendo que se destruya inmediatamente y solo se vea el efecto de hit.
        // El impacto visual se resuelve por distancia al targetPoint en Update().
    }

    private void Impact()
    {
        ImpactAt(transform.position, transform.rotation);
    }

    private void ImpactAt(Vector3 impactPosition, Quaternion impactRotation)
    {
        if (impactPrefab != null)
            Instantiate(impactPrefab, impactPosition, impactRotation);

        if (destroyOnImpact)
            Destroy(gameObject);
    }
}