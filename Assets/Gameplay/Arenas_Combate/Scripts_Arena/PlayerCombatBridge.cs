using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerCombatBridge : NetworkBehaviour
{
    [SerializeField] private PlayerNameDisplay playerNameDisplay;
    [SerializeField] private PlayerPlantLoadout playerPlantLoadout;

    private void Awake()
    {
        if (playerNameDisplay == null)
            playerNameDisplay = GetComponentInChildren<PlayerNameDisplay>(true);

        if (playerPlantLoadout == null)
            playerPlantLoadout = GetComponent<PlayerPlantLoadout>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        StartCoroutine(BindUILater());
    }

    private IEnumerator BindUILater()
    {
        while (CombatUIController.LocalInstance == null)
            yield return null;

        CombatUIController.LocalInstance.BindBridge(this);
    }

    public void SubmitLocalAction(CombatActionType actionType)
    {
        if (!IsOwner) return;
        SubmitActionServerRpc((int)actionType);
    }

    [ServerRpc]
    private void SubmitActionServerRpc(int actionType, ServerRpcParams rpcParams = default)
    {
        DuelCombatManager.Instance?.ReceivePlayerAction(rpcParams.Receive.SenderClientId, (CombatActionType)actionType);
    }

    [ClientRpc]
    public void ShowCombatUIClientRpc(
        string myName,
        string rivalName,
        int myCurrentHP,
        int myMaxHP,
        int rivalCurrentHP,
        int rivalMaxHP,
        string basicAttackName,
        string defenseSkillName,
        string specialSkillName,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        CombatUIController.LocalInstance?.ShowCombatUI(
            myName,
            rivalName,
            myCurrentHP,
            myMaxHP,
            rivalCurrentHP,
            rivalMaxHP,
            basicAttackName,
            defenseSkillName,
            specialSkillName
        );
    }

    [ClientRpc]
    public void UpdateCombatUIClientRpc(
        int myCurrentHP,
        int myMaxHP,
        int rivalCurrentHP,
        int rivalMaxHP,
        int secondsRemaining,
        bool canBasic,
        bool canDefense,
        bool canSpecial,
        int basicCooldownSeconds,
        int specialCooldownSeconds,
        int defenseUsesRemaining,
        string statusMessage,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        CombatUIController.LocalInstance?.UpdateCombatState(
            myCurrentHP,
            myMaxHP,
            rivalCurrentHP,
            rivalMaxHP,
            secondsRemaining,
            canBasic,
            canDefense,
            canSpecial,
            basicCooldownSeconds,
            specialCooldownSeconds,
            defenseUsesRemaining,
            statusMessage
        );
    }

    [ClientRpc]
    public void HideCombatUIClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        CombatUIController.LocalInstance?.HideCombatUI();
    }

    public string GetDisplayName()
    {
        if (playerNameDisplay == null) return $"Player_{OwnerClientId}";
        string n = playerNameDisplay.GetPlayerName();
        return string.IsNullOrWhiteSpace(n) ? $"Player_{OwnerClientId}" : n;
    }

    public PlayerPlantLoadout GetLoadout()
    {
        return playerPlantLoadout;
    }
}