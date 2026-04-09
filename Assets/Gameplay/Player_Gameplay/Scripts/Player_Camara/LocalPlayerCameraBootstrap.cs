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
}


/*using UnityEngine;
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
        localCameraInstance.SetTarget(cameraTarget, true);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (localCameraInstance != null)
            Destroy(localCameraInstance.gameObject);
    }
}*/