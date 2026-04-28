using UnityEngine;
using Unity.Netcode;

public class LocalPlayerCameraBootstrap : NetworkBehaviour
{
    [SerializeField] private LocalFollowCamera cameraPrefab;
    [SerializeField] private Transform cameraTarget;

    private LocalFollowCamera localCameraInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (cameraTarget == null)
            cameraTarget = transform;

        localCameraInstance = Instantiate(cameraPrefab);
        localCameraInstance.SetFollowTarget(cameraTarget, true);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (localCameraInstance != null)
            Destroy(localCameraInstance.gameObject);
    }

    public void EnterCombatCamera(Transform combatTarget = null)
    {
        if (!IsOwner || localCameraInstance == null) return;

        Transform targetToUse = combatTarget != null ? combatTarget : cameraTarget;
        localCameraInstance.SetCombatOrbit(targetToUse, true);
    }

    public void ExitCombatCamera()
    {
        if (!IsOwner || localCameraInstance == null) return;

        localCameraInstance.SetFollowTarget(cameraTarget, true);
    }
}