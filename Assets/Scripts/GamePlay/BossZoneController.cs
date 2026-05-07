using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class BossZoneController : NetworkBehaviour
{
    [Header("Identidad del Boss")]
    [SerializeField] private string bossId = "boss_hidro";
    [SerializeField] private string bossDisplayName = "Guardián del Humedal";
    [SerializeField] private PlantBiomeType bossBiome = PlantBiomeType.Hidro;

    [Header("Zona permitida")]
    [Tooltip("Zona lógica donde este boss puede moverse. Debe tener BiomeZone.")]
    [SerializeField] private BiomeZone assignedZone;

    [Header("Movimiento aleatorio")]
    [SerializeField] private bool canMove = true;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 7f;
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private float minDistanceToTarget = 1.2f;
    [SerializeField] private float randomPointYOffset = 0f;

    [Header("Detección del Player")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool stopWhenPlayerIsNear = true;

    [Header("Objeto visual interno")]
    [Tooltip("Objeto hijo que contiene el modelo 3D del boss.")]
    [SerializeField] private Transform modelRoot;

    [Header("Animación simulada al moverse")]
    [SerializeField] private float walkBobAmplitude = 0.15f;
    [SerializeField] private float walkBobFrequency = 4f;
    [SerializeField] private float walkTiltZAmplitude = 6f;
    [SerializeField] private float walkTiltZFrequency = 4f;

    [Header("Animación simulada en reposo")]
    [SerializeField] private float idleRotationYAmplitude = 8f;
    [SerializeField] private float idleRotationYFrequency = 1.5f;
    [SerializeField] private float idleBobAmplitude = 0.05f;
    [SerializeField] private float idleBobFrequency = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private Color debugColor = new Color(1f, 0.4f, 0f, 0.35f);

    [Header("Sincronización de red")]
    [SerializeField] private bool useServerAuthoritativeMovement = true;
    [SerializeField] private float clientPositionLerpSpeed = 12f;
    [SerializeField] private float clientRotationLerpSpeed = 12f;
    [SerializeField] private float networkSendInterval = 0.05f;

    private Rigidbody rb;
    private SphereCollider detectionCollider;

    private Vector3 targetPosition;
    private float initialWorldY;
    private Vector3 modelInitialLocalPosition;
    private Quaternion modelInitialLocalRotation;

    private float waitTimer;
    private bool hasTarget;
    private bool playerIsNear;
    private Transform detectedPlayer;

    private float networkSendTimer;

    private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> networkPlayerIsNear = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public string BossId => bossId;
    public string BossDisplayName => bossDisplayName;
    public PlantBiomeType BossBiome => bossBiome;
    public bool PlayerIsNear => playerIsNear;
    public Transform DetectedPlayer => detectedPlayer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        detectionCollider = GetComponent<SphereCollider>();
        initialWorldY = transform.position.y;

        ConfigurePhysics();

        if (modelRoot != null)
        {
            modelInitialLocalPosition = modelRoot.localPosition;
            modelInitialLocalRotation = modelRoot.localRotation;
        }
    }

    private void Start()
    {
        if (assignedZone == null)
        {
            Debug.LogWarning($"{name}: No tiene una BiomeZone asignada. El boss no podrá limitar su movimiento correctamente.");
            return;
        }

        PickNewRandomTarget();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
            networkPlayerIsNear.Value = playerIsNear;
        }
        else
        {
            transform.position = networkPosition.Value;
            transform.rotation = networkRotation.Value;
            playerIsNear = networkPlayerIsNear.Value;
        }
    }

    private void Update()
    {
        if (useServerAuthoritativeMovement && IsSpawned && !IsServer)
        {
            ApplyNetworkTransformOnClient();
            playerIsNear = networkPlayerIsNear.Value;
            AnimateModel();
            return;
        }

        if (!canMove)
        {
            AnimateIdle();
            SyncNetworkStateIfNeeded();
            return;
        }

        if (playerIsNear && stopWhenPlayerIsNear)
        {
            FacePlayer();
            AnimateIdle();
            SyncNetworkStateIfNeeded();
            return;
        }

        MoveInsideZone();
        AnimateModel();
        SyncNetworkStateIfNeeded();
    }

    private void ApplyNetworkTransformOnClient()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            networkPosition.Value,
            clientPositionLerpSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            networkRotation.Value,
            clientRotationLerpSpeed * Time.deltaTime
        );
    }

    private void SyncNetworkStateIfNeeded()
    {
        if (!useServerAuthoritativeMovement)
            return;

        if (!IsSpawned || !IsServer)
            return;

        networkSendTimer += Time.deltaTime;

        if (networkSendTimer < networkSendInterval)
            return;

        networkSendTimer = 0f;
        networkPosition.Value = transform.position;
        networkRotation.Value = transform.rotation;
        networkPlayerIsNear.Value = playerIsNear;
    }

    private void ConfigurePhysics()
    {
        if (detectionCollider != null)
            detectionCollider.isTrigger = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void MoveInsideZone()
    {
        if (assignedZone == null)
            return;

        if (!hasTarget)
        {
            PickNewRandomTarget();
            return;
        }

        Vector3 currentPosition = ClampPositionToInitialY(transform.position);
        Vector3 flatTarget = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);
        Vector3 direction = flatTarget - currentPosition;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= minDistanceToTarget)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTimeAtPoint)
            {
                waitTimer = 0f;
                PickNewRandomTarget();
            }

            return;
        }

        Vector3 moveDirection = direction.normalized;
        Vector3 nextPosition = currentPosition + moveDirection * moveSpeed * Time.deltaTime;

        nextPosition = ClampPositionToInitialY(nextPosition);

        if (assignedZone.ContainsPoint(nextPosition))
        {
            transform.position = nextPosition;
        }
        else
        {
            PickNewRandomTarget();
            return;
        }

        RotateTowards(moveDirection);
    }

    private void PickNewRandomTarget()
    {
        if (assignedZone == null)
        {
            hasTarget = false;
            return;
        }

        for (int i = 0; i < 20; i++)
        {
            Vector3 candidate = assignedZone.GetRandomPointInsideZone(randomPointYOffset);
            candidate.y = initialWorldY;

            if (assignedZone.ContainsPoint(candidate))
            {
                targetPosition = candidate;
                hasTarget = true;
                return;
            }
        }

        targetPosition = assignedZone.GetZoneCenter();
        targetPosition.y = initialWorldY;
        hasTarget = true;
    }

    private Vector3 ClampPositionToInitialY(Vector3 position)
    {
        if (position.y < initialWorldY)
            position.y = initialWorldY;

        return position;
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void FacePlayer()
    {
        if (detectedPlayer == null)
            return;

        Vector3 direction = detectedPlayer.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        RotateTowards(direction.normalized);
    }

    private void AnimateModel()
    {
        if (modelRoot == null)
            return;

        bool isWalking = !playerIsNear && hasTarget;

        if (isWalking)
            AnimateWalking();
        else
            AnimateIdle();
    }

    private void AnimateWalking()
    {
        float bob = Mathf.Sin(Time.time * walkBobFrequency) * walkBobAmplitude;
        float tiltZ = Mathf.Sin(Time.time * walkTiltZFrequency) * walkTiltZAmplitude;

        modelRoot.localPosition = modelInitialLocalPosition + new Vector3(0f, bob, 0f);
        modelRoot.localRotation = modelInitialLocalRotation * Quaternion.Euler(0f, 0f, tiltZ);
    }

    private void AnimateIdle()
    {
        if (modelRoot == null)
            return;

        float bob = Mathf.Sin(Time.time * idleBobFrequency) * idleBobAmplitude;
        float rotY = Mathf.Sin(Time.time * idleRotationYFrequency) * idleRotationYAmplitude;

        modelRoot.localPosition = modelInitialLocalPosition + new Vector3(0f, bob, 0f);
        modelRoot.localRotation = modelInitialLocalRotation * Quaternion.Euler(0f, rotY, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (useServerAuthoritativeMovement && IsSpawned && !IsServer)
            return;

        if (!other.CompareTag(playerTag))
            return;

        playerIsNear = true;
        detectedPlayer = other.transform;
        if (IsSpawned && IsServer)
            networkPlayerIsNear.Value = true;

        Debug.Log($"{bossDisplayName} detectó al jugador. Preparar reto de combate.");
    }

    private void OnTriggerStay(Collider other)
    {
        if (useServerAuthoritativeMovement && IsSpawned && !IsServer)
            return;

        if (!other.CompareTag(playerTag))
            return;

        playerIsNear = true;
        detectedPlayer = other.transform;
        if (IsSpawned && IsServer)
            networkPlayerIsNear.Value = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (useServerAuthoritativeMovement && IsSpawned && !IsServer)
            return;

        if (!other.CompareTag(playerTag))
            return;

        playerIsNear = false;
        detectedPlayer = null;
        if (IsSpawned && IsServer)
            networkPlayerIsNear.Value = false;

        waitTimer = 0f;
        PickNewRandomTarget();

        Debug.Log($"{bossDisplayName} dejó de detectar al jugador. Retoma patrullaje.");
    }

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
    }

    public void AssignZone(BiomeZone zone)
    {
        assignedZone = zone;
        PickNewRandomTarget();
    }

    public void ForceStopForCombat()
    {
        playerIsNear = true;
        canMove = false;
        if (IsSpawned && IsServer)
        {
            networkPlayerIsNear.Value = true;
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }
    }

    public void ResumeMovement()
    {
        canMove = true;
        playerIsNear = false;
        detectedPlayer = null;
        if (IsSpawned && IsServer)
            networkPlayerIsNear.Value = false;
        PickNewRandomTarget();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        SphereCollider sphere = GetComponent<SphereCollider>();

        if (sphere != null)
        {
            Gizmos.color = debugColor;

            Vector3 worldCenter = transform.TransformPoint(sphere.center);
            float maxScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z)
            );

            float worldRadius = sphere.radius * maxScale;

            Gizmos.DrawSphere(worldCenter, worldRadius);
            Gizmos.DrawWireSphere(worldCenter, worldRadius);
        }

        if (hasTarget)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(targetPosition, 0.4f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
}