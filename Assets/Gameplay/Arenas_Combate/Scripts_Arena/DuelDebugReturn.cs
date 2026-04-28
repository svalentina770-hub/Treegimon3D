using UnityEngine;
using Unity.Netcode;

public class DuelDebugReturn : NetworkBehaviour
{
    [SerializeField] private KeyCode returnKey = KeyCode.F8;

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(returnKey))
        {
            RequestReturnServerRpc();
        }
    }

    [ServerRpc]
    private void RequestReturnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (DuelArenaManager.Instance != null)
            DuelArenaManager.Instance.TryEndDuelByPlayer(OwnerClientId);
    }
}
