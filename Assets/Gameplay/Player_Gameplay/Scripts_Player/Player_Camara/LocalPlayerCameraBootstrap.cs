using UnityEngine;
using Unity.Netcode;

public class LocalPlayerCameraBootstrap : NetworkBehaviour
{
    [Header("Prefab de cámara local")]
    [SerializeField] private SmoothCameraFollow cameraPrefab;

    [Header("Objetivo de cámara")]
    [SerializeField] private Transform cameraTarget;

    private SmoothCameraFollow localCameraInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        if (cameraTarget == null)
            cameraTarget = transform;

        if (cameraPrefab == null)
        {
            Debug.LogWarning("[LocalPlayerCameraBootstrap] No hay prefab de cámara asignado.");
            return;
        }

        localCameraInstance = Instantiate(cameraPrefab);
        localCameraInstance.SetTarget(cameraTarget);

        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
            TryAssignCameraToPlayerMovement(playerMovement, localCameraInstance.transform);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        if (localCameraInstance != null)
            Destroy(localCameraInstance.gameObject);
    }

    public void EnterCombatCamera(Transform combatTarget = null)
    {
        if (!IsOwner || localCameraInstance == null)
            return;

        Transform targetToUse = combatTarget != null ? combatTarget : cameraTarget;
        localCameraInstance.SetTarget(targetToUse);
    }

    public void ExitCombatCamera()
    {
        if (!IsOwner || localCameraInstance == null)
            return;

        localCameraInstance.SetTarget(cameraTarget);
    }

    private void TryAssignCameraToPlayerMovement(PlayerMovement playerMovement, Transform cameraTransform)
    {
        System.Reflection.MethodInfo method = typeof(PlayerMovement).GetMethod("SetCameraTransform");

        if (method != null)
        {
            method.Invoke(playerMovement, new object[] { cameraTransform });
            return;
        }

        Debug.LogWarning("[LocalPlayerCameraBootstrap] PlayerMovement no tiene SetCameraTransform(). La cámara seguirá al jugador, pero el movimiento no será relativo a la cámara hasta agregar ese método al PlayerMovement real.");
    }
}