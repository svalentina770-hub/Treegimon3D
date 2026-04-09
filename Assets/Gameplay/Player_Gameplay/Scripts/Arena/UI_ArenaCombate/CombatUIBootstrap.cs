using UnityEngine;
using Unity.Netcode;

public class CombatUIBootstrap : NetworkBehaviour
{
    [SerializeField] private CombatUIController combatUIPrefab;

    private CombatUIController localUIInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (combatUIPrefab == null)
        {
            Debug.LogWarning("[CombatUIBootstrap] No hay Combat UI Prefab asignado.");
            return;
        }

        localUIInstance = Instantiate(combatUIPrefab);

        if (localUIInstance != null)
            localUIInstance.HideCombatUI();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (localUIInstance != null)
            Destroy(localUIInstance.gameObject);
    }
}