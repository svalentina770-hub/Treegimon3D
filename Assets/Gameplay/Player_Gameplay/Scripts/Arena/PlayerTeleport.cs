using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerTeleport : NetworkBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private MonoBehaviour movementScript;
    [SerializeField] private Transform playerCameraTarget;
    [SerializeField] private CombatArenaInstance localArenaVisualPrefab;
    [SerializeField] private SphereCollider proximityTrigger;
    [SerializeField] private ChallengeSystem challengeSystem;

    private bool wasUsingGravity;
    private bool wasKinematic;
    private bool wasMovementEnabled;

    private CombatArenaInstance localArenaInstance;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (movementScript == null)
            movementScript = GetComponent<PlayerMovement>();

        if (proximityTrigger == null)
            proximityTrigger = GetComponent<SphereCollider>();

        if (challengeSystem == null)
            challengeSystem = GetComponent<ChallengeSystem>();
    }

    public void EnterDuelMode(
        Vector3 playerPosition,
        Quaternion playerRotation,
        Vector3 arenaPosition,
        Quaternion arenaRotation,
        string myName,
        string rivalName)
    {
        ApplyEnterDuelMode(playerPosition, playerRotation, arenaPosition, arenaRotation, myName, rivalName);

        if (IsServer)
        {
            EnterDuelModeClientRpc(
                playerPosition,
                playerRotation.eulerAngles,
                arenaPosition,
                arenaRotation.eulerAngles,
                myName,
                rivalName,
                BuildTargetRpcParams(OwnerClientId)
            );
        }
    }

    public void ExitDuelMode(Vector3 returnPosition, Quaternion returnRotation)
    {
        ApplyExitDuelMode(returnPosition, returnRotation);

        if (IsServer)
        {
            ExitDuelModeClientRpc(
                returnPosition,
                returnRotation.eulerAngles,
                BuildTargetRpcParams(OwnerClientId)
            );
        }
    }

    private void ApplyEnterDuelMode(
        Vector3 playerPosition,
        Quaternion playerRotation,
        Vector3 arenaPosition,
        Quaternion arenaRotation,
        string myName,
        string rivalName)
    {
        if (movementScript != null)
        {
            wasMovementEnabled = movementScript.enabled;
            movementScript.enabled = false;
        }

        if (rb != null)
        {
            wasUsingGravity = rb.useGravity;
            wasKinematic = rb.isKinematic;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        transform.SetPositionAndRotation(playerPosition, playerRotation);
        Physics.SyncTransforms();

        if (IsOwner)
        {
            if (localArenaVisualPrefab != null && localArenaInstance == null)
                localArenaInstance = Instantiate(localArenaVisualPrefab, arenaPosition, arenaRotation);

            LocalFollowCamera cam = Camera.main != null ? Camera.main.GetComponent<LocalFollowCamera>() : null;
            if (cam != null)
            {
                if (localArenaInstance != null && localArenaInstance.CameraAnchor != null)
                    cam.SetFixedAnchor(localArenaInstance.CameraAnchor, true);
                else
                    cam.SnapNow();
            }

            if (CombatUIController.LocalInstance != null)
            {
                CombatUIController.LocalInstance.ShowCombatUI(
                    myName,
                    rivalName,
                    100, 100,
                    100, 100
                );
            }
        }
    }

    private void ApplyExitDuelMode(Vector3 returnPosition, Quaternion returnRotation)
    {
        transform.SetPositionAndRotation(returnPosition, returnRotation);
        Physics.SyncTransforms();

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.useGravity = wasUsingGravity;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (movementScript != null)
            movementScript.enabled = wasMovementEnabled;

        if (IsOwner)
        {
            if (localArenaInstance != null)
            {
                Destroy(localArenaInstance.gameObject);
                localArenaInstance = null;
            }

            LocalFollowCamera cam = Camera.main != null ? Camera.main.GetComponent<LocalFollowCamera>() : null;
            if (cam != null && playerCameraTarget != null)
                cam.SetFollowTarget(playerCameraTarget, true);

            if (CombatUIController.LocalInstance != null)
                CombatUIController.LocalInstance.HideCombatUI();
        }

        if (challengeSystem != null)
            challengeSystem.ForceResetAfterDuelLocal();

        StartCoroutine(RearmProximityTrigger());
    }

    private IEnumerator RearmProximityTrigger()
    {
        if (proximityTrigger == null)
            yield break;

        proximityTrigger.enabled = false;
        yield return null;
        yield return new WaitForFixedUpdate();
        proximityTrigger.enabled = true;
    }

    [ClientRpc]
    private void EnterDuelModeClientRpc(
        Vector3 playerPosition,
        Vector3 playerEuler,
        Vector3 arenaPosition,
        Vector3 arenaEuler,
        string myName,
        string rivalName,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        ApplyEnterDuelMode(
            playerPosition,
            Quaternion.Euler(playerEuler),
            arenaPosition,
            Quaternion.Euler(arenaEuler),
            myName,
            rivalName
        );
    }

    [ClientRpc]
    private void ExitDuelModeClientRpc(
        Vector3 returnPosition,
        Vector3 returnEuler,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        ApplyExitDuelMode(
            returnPosition,
            Quaternion.Euler(returnEuler)
        );
    }

    private static ClientRpcParams BuildTargetRpcParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }
}