using UnityEngine;
using Unity.Netcode;

public class CombatUIBootstrap : NetworkBehaviour
{
    [Header("Prefab de UI de combate")]
    [SerializeField] private CombatUIController combatUIPrefab;

    [Header("Opciones")]
    [SerializeField] private bool logWarningIfMissingPrefab = true;

    private CombatUIController localUIInstance;
    private bool uiAvailable;

    public bool UIAvailable => uiAvailable && localUIInstance != null;
    public CombatUIController LocalUIInstance => localUIInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        TryCreateLocalCombatUI();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        DestroyLocalCombatUI();
    }

    public bool TryCreateLocalCombatUI()
    {
        if (!IsOwner)
            return false;

        if (localUIInstance != null)
        {
            uiAvailable = true;
            return true;
        }

        if (combatUIPrefab == null)
        {
            uiAvailable = false;

            if (logWarningIfMissingPrefab)
                Debug.LogWarning("[CombatUIBootstrap] No hay Combat UI Prefab asignado. El combate puede continuar sin interfaz visual temporalmente.");

            return false;
        }

        localUIInstance = Instantiate(combatUIPrefab);
        uiAvailable = localUIInstance != null;

        if (localUIInstance != null)
            localUIInstance.HideCombatUI();

        return uiAvailable;
    }

    public void SafeShowCombatUI(
        string myName,
        string rivalName,
        int myCurrentHp,
        int myMaxHp,
        int rivalCurrentHp,
        int rivalMaxHp,
        string attackName,
        string defendName,
        string specialName)
    {
        if (!TryCreateLocalCombatUI())
            return;

        localUIInstance.ShowCombatUI(
            myName,
            rivalName,
            myCurrentHp,
            myMaxHp,
            rivalCurrentHp,
            rivalMaxHp,
            attackName,
            defendName,
            specialName
        );
    }

    public void SafeShowCombatUI()
    {
        Debug.LogWarning("[CombatUIBootstrap] SafeShowCombatUI() fue llamado sin datos de combate. Usa la sobrecarga con nombres, HP y nombres de habilidades.");
    }

    public void SafeHideCombatUI()
    {
        if (localUIInstance == null)
            return;

        localUIInstance.HideCombatUI();
    }

    private void DestroyLocalCombatUI()
    {
        if (localUIInstance == null)
            return;

        Destroy(localUIInstance.gameObject);
        localUIInstance = null;
        uiAvailable = false;
    }
}