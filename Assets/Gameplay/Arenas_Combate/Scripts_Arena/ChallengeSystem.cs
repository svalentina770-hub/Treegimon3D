using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class ChallengeSystem : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas localChallengeCanvas;
    [SerializeField] private GameObject challengeUI;
    [SerializeField] private TextMeshProUGUI rivalNameText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    private TextMeshProUGUI acceptButtonText;
    private TextMeshProUGUI rejectButtonText;

    private bool hasCurrentTarget;
    private ulong currentTargetClientId;

    private bool hasNearbyRival;
    private ulong nearbyRivalClientId;
    private string nearbyRivalName;

    private LocalState localState = LocalState.None;

    private enum LocalState
    {
        None,
        NearbyPrompt,
        WaitingResponse,
        IncomingRequest
    }

    private static readonly Dictionary<ulong, ulong> pendingByChallenger = new();
    private static readonly Dictionary<ulong, ulong> pendingByTarget = new();

    private void Awake()
    {
        if (acceptButton != null)
            acceptButtonText = acceptButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (rejectButton != null)
            rejectButtonText = rejectButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (localChallengeCanvas == null || challengeUI == null || rivalNameText == null || acceptButton == null || rejectButton == null)
        {
            Debug.LogError($"[ChallengeSystem] Referencias faltantes en {gameObject.name}");
            enabled = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (localChallengeCanvas != null)
        {
            localChallengeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            localChallengeCanvas.gameObject.SetActive(IsOwner);
        }

        HideUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;
        if (localState != LocalState.None) return;

        if (DuelArenaManager.Instance != null && DuelArenaManager.Instance.IsPlayerBusy(OwnerClientId))
            return;

        PlayerNameDisplay rival = ResolveRival(other);
        if (rival == null) return;
        if (rival.OwnerClientId == OwnerClientId) return;

        if (DuelArenaManager.Instance != null && DuelArenaManager.Instance.IsPlayerBusy(rival.OwnerClientId))
            return;

        hasNearbyRival = true;
        nearbyRivalClientId = rival.OwnerClientId;
        nearbyRivalName = rival.GetPlayerName();

        PlayerPlantLoadout myLoadout = GetComponent<PlayerPlantLoadout>();
        PlayerPlantLoadout rivalLoadout = rival.GetComponentInParent<PlayerPlantLoadout>();

        if (myLoadout != null && !myLoadout.CanChallenge())
        {
            Debug.Log("Tu planta aún no cumple el nivel mínimo para PvP.");
            return;
        }

        if (rivalLoadout != null && !rivalLoadout.CanChallenge())
        {
            Debug.Log("La planta del rival aún no cumple el nivel mínimo para PvP.");
            return;
        }


        ShowNearbyPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        PlayerNameDisplay rival = ResolveRival(other);
        if (rival == null) return;

        if (hasNearbyRival && rival.OwnerClientId == nearbyRivalClientId)
        {
            hasNearbyRival = false;
            nearbyRivalClientId = 0;
            nearbyRivalName = "";

            if (localState == LocalState.NearbyPrompt)
                HideUI();
        }
    }

    private PlayerNameDisplay ResolveRival(Collider other)
    {
        NetworkObject otherNetworkObject = other.GetComponentInParent<NetworkObject>();
        if (otherNetworkObject == null && other.attachedRigidbody != null)
            otherNetworkObject = other.attachedRigidbody.GetComponent<NetworkObject>();

        if (otherNetworkObject == null)
            return null;

        return otherNetworkObject.GetComponentInChildren<PlayerNameDisplay>(true);
    }

    private void ShowNearbyPrompt()
    {
        if (!hasNearbyRival) return;
        if (localState != LocalState.None) return;

        hasCurrentTarget = true;
        currentTargetClientId = nearbyRivalClientId;
        localState = LocalState.NearbyPrompt;

        rivalNameText.text = $"Retar a: {nearbyRivalName}";
        SetButtonLabels("Retar", "Cerrar");

        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();

        acceptButton.onClick.AddListener(SendCurrentChallenge);
        rejectButton.onClick.AddListener(HideUI);

        challengeUI.SetActive(true);
    }

    private void SetButtonLabels(string left, string right)
    {
        if (acceptButtonText != null) acceptButtonText.text = left;
        if (rejectButtonText != null) rejectButtonText.text = right;
    }

    private void HideUI()
    {
        hasCurrentTarget = false;
        currentTargetClientId = 0;
        localState = LocalState.None;

        if (acceptButton != null) acceptButton.onClick.RemoveAllListeners();
        if (rejectButton != null) rejectButton.onClick.RemoveAllListeners();

        if (challengeUI != null)
            challengeUI.SetActive(false);
    }

    public void ForceResetAfterDuelLocal()
    {
        HideUI();
        hasNearbyRival = false;
        nearbyRivalClientId = 0;
        nearbyRivalName = "";
    }

    [ClientRpc]
    public void ForceResetAfterDuelClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        ForceResetAfterDuelLocal();
    }

    private void SendCurrentChallenge()
    {
        if (!hasCurrentTarget) return;

        ulong target = currentTargetClientId;

        localState = LocalState.WaitingResponse;
        challengeUI.SetActive(false);

        SendChallengeServerRpc(target);
    }

    [ServerRpc]
    private void SendChallengeServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        ulong challengerId = rpcParams.Receive.SenderClientId;

        if (challengerId == targetClientId)
        {
            SendFailureToClient(challengerId, "No puedes retarte a ti mismo.");
            return;
        }

        if (DuelArenaManager.Instance != null &&
            (DuelArenaManager.Instance.IsPlayerBusy(challengerId) ||
             DuelArenaManager.Instance.IsPlayerBusy(targetClientId)))
        {
            SendFailureToClient(challengerId, "Uno de los jugadores ya está en un duelo.");
            return;
        }

        if (pendingByChallenger.ContainsKey(challengerId) || pendingByTarget.ContainsKey(challengerId)
            || pendingByChallenger.ContainsKey(targetClientId) || pendingByTarget.ContainsKey(targetClientId))
        {
            SendFailureToClient(challengerId, "Uno de los jugadores ya tiene un reto pendiente.");
            return;
        }

        ChallengeSystem challengerSystem = GetChallengeSystem(challengerId);
        ChallengeSystem targetSystem = GetChallengeSystem(targetClientId);

        if (challengerSystem == null || targetSystem == null)
        {
            SendFailureToClient(challengerId, "No se encontró el sistema de reto.");
            return;
        }

        string challengerName = GetPlayerNameByClientId(challengerId);
        string targetName = GetPlayerNameByClientId(targetClientId);

        pendingByChallenger[challengerId] = targetClientId;
        pendingByTarget[targetClientId] = challengerId;

        challengerSystem.ShowWaitingClientRpc(targetName, GetClientRpcParams(challengerId));
        targetSystem.ReceiveChallengeClientRpc(challengerId, challengerName, GetClientRpcParams(targetClientId));
    }

    [ClientRpc]
    private void ShowWaitingClientRpc(string targetName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        localState = LocalState.WaitingResponse;
        Debug.Log($"Esperando respuesta de {targetName}...");
    }

    [ClientRpc]
    private void ReceiveChallengeClientRpc(ulong challengerId, string challengerName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        hasCurrentTarget = true;
        currentTargetClientId = challengerId;
        localState = LocalState.IncomingRequest;

        rivalNameText.text = $"{challengerName} te ha retado";
        SetButtonLabels("Aceptar", "Rechazar");

        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();

        acceptButton.onClick.AddListener(AcceptIncomingChallenge);
        rejectButton.onClick.AddListener(RejectIncomingChallenge);

        challengeUI.SetActive(true);
    }

    private void AcceptIncomingChallenge()
    {
        if (!hasCurrentTarget) return;

        ulong challenger = currentTargetClientId;
        challengeUI.SetActive(false);

        RespondToChallengeServerRpc(challenger, true);
    }

    private void RejectIncomingChallenge()
    {
        if (!hasCurrentTarget) return;

        ulong challenger = currentTargetClientId;
        challengeUI.SetActive(false);

        RespondToChallengeServerRpc(challenger, false);
    }

    [ServerRpc]
    private void RespondToChallengeServerRpc(ulong challengerId, bool accepted, ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        if (!pendingByTarget.TryGetValue(responderId, out ulong registeredChallenger))
            return;

        if (registeredChallenger != challengerId)
            return;

        pendingByTarget.Remove(responderId);
        pendingByChallenger.Remove(challengerId);

        ChallengeSystem challengerSystem = GetChallengeSystem(challengerId);
        ChallengeSystem responderSystem = GetChallengeSystem(responderId);

        if (challengerSystem == null || responderSystem == null)
            return;

        string responderName = GetPlayerNameByClientId(responderId);
        string challengerName = GetPlayerNameByClientId(challengerId);

        if (!accepted)
        {
            challengerSystem.NotifyChallengeRejectedClientRpc(responderName, GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        if (DuelArenaManager.Instance == null)
        {
            challengerSystem.NotifyGenericMessageClientRpc("No existe DuelArenaManager en la escena.", GetClientRpcParams(challengerId));
            responderSystem.NotifyGenericMessageClientRpc("No existe DuelArenaManager en la escena.", GetClientRpcParams(responderId));

            challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        bool started = DuelArenaManager.Instance.TryStartDuel(challengerId, responderId, out int duelId);

        if (!started)
        {
            challengerSystem.NotifyGenericMessageClientRpc("No se pudo iniciar el duelo.", GetClientRpcParams(challengerId));
            responderSystem.NotifyGenericMessageClientRpc("No se pudo iniciar el duelo.", GetClientRpcParams(responderId));

            challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        challengerSystem.NotifyChallengeAcceptedClientRpc(responderName, GetClientRpcParams(challengerId));
        responderSystem.NotifyChallengeAcceptedClientRpc(challengerName, GetClientRpcParams(responderId));

        challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
        responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
    }

    [ClientRpc]
    private void NotifyChallengeAcceptedClientRpc(string otherPlayerName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log($"{otherPlayerName} aceptó el reto.");
    }

    [ClientRpc]
    private void NotifyChallengeRejectedClientRpc(string otherPlayerName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        Debug.Log($"{otherPlayerName} rechazó el reto.");
        HideUI();

        if (hasNearbyRival)
            ShowNearbyPrompt();
    }

    [ClientRpc]
    private void NotifyGenericMessageClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log(message);
    }

    [ClientRpc]
    private void ResetLocalStateClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        HideUI();
    }

    private void SendFailureToClient(ulong clientId, string message)
    {
        ChallengeSystem system = GetChallengeSystem(clientId);
        if (system == null) return;

        system.NotifyGenericMessageClientRpc(message, GetClientRpcParams(clientId));
        system.ResetLocalStateClientRpc(GetClientRpcParams(clientId));
    }

    private static ChallengeSystem GetChallengeSystem(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            return null;

        if (clientData.PlayerObject == null)
            return null;

        ChallengeSystem system = clientData.PlayerObject.GetComponent<ChallengeSystem>();
        if (system == null)
            system = clientData.PlayerObject.GetComponentInChildren<ChallengeSystem>(true);

        return system;
    }

    private static string GetPlayerNameByClientId(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return $"Player_{clientId}";

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            return $"Player_{clientId}";

        if (clientData.PlayerObject == null)
            return $"Player_{clientId}";

        PlayerNameDisplay display = clientData.PlayerObject.GetComponentInChildren<PlayerNameDisplay>(true);
        if (display == null)
            return $"Player_{clientId}";

        string finalName = display.GetPlayerName();

        if (string.IsNullOrWhiteSpace(finalName))
            finalName = $"Player_{clientId}";

        return finalName;
    }

    private static ClientRpcParams GetClientRpcParams(ulong targetClientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };
    }
}








/*using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class ChallengeSystem : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject challengeUI;
    [SerializeField] private TextMeshProUGUI rivalNameText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    private TextMeshProUGUI acceptButtonText;
    private TextMeshProUGUI rejectButtonText;

    private ulong currentTargetClientId;
    private LocalState localState = LocalState.None;

    private enum LocalState
    {
        None,
        NearbyPrompt,
        WaitingResponse,
        IncomingRequest,
        InDuel
    }

    // Servidor: quién retó a quién
    private static readonly Dictionary<ulong, ulong> pendingByChallenger = new(); // challenger -> target
    private static readonly Dictionary<ulong, ulong> pendingByTarget = new();      // target -> challenger
    private static readonly HashSet<ulong> duelLockedPlayers = new();              // jugadores ocupados en duelo

    private void Awake()
    {
        if (acceptButton != null)
            acceptButtonText = acceptButton.GetComponentInChildren<TextMeshProUGUI>();

        if (rejectButton != null)
            rejectButtonText = rejectButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    public override void OnNetworkSpawn()
    {
        HideUI();
    }

    private void OnTriggerEnter(Collider other)
    {

        //No permitir reto si ya están en duelo
        if (DuelArenaManager.Instance != null && DuelArenaManager.Instance.IsPlayerBusy(OwnerClientId))
            return;

        if (!IsOwner) return;
        if (localState != LocalState.None) return;

        PlayerNameDisplay rival = ResolveRival(other);
        if (rival == null) return;
        if (rival.OwnerClientId == OwnerClientId) return;

        if (DuelArenaManager.Instance != null && DuelArenaManager.Instance.IsPlayerBusy(rival.OwnerClientId))
            return;

        currentTargetClientId = rival.OwnerClientId;
        localState = LocalState.NearbyPrompt;

        rivalNameText.text = $"Retar a: {rival.GetPlayerName()}";
        SetButtonLabels("Retar", "Cerrar");

        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();

        acceptButton.onClick.AddListener(SendCurrentChallenge);
        rejectButton.onClick.AddListener(HideUI);

        challengeUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;
        if (localState != LocalState.NearbyPrompt) return;

        PlayerNameDisplay rival = ResolveRival(other);
        if (rival == null) return;

        if (rival.OwnerClientId == currentTargetClientId)
        {
            HideUI();
        }
    }

    private PlayerNameDisplay ResolveRival(Collider other)
    {
        PlayerNameDisplay rival = other.GetComponent<PlayerNameDisplay>();
        if (rival == null) rival = other.GetComponentInParent<PlayerNameDisplay>();
        if (rival == null) rival = other.GetComponentInChildren<PlayerNameDisplay>();
        return rival;
    }

    private void SetButtonLabels(string left, string right)
    {
        if (acceptButtonText != null) acceptButtonText.text = left;
        if (rejectButtonText != null) rejectButtonText.text = right;
    }

    private void HideUI()
    {
        currentTargetClientId = 0;
        localState = LocalState.None;

        if (acceptButton != null) acceptButton.onClick.RemoveAllListeners();
        if (rejectButton != null) rejectButton.onClick.RemoveAllListeners();

        if (challengeUI != null) challengeUI.SetActive(false);
    }

    private void SendCurrentChallenge()
    {
        if (currentTargetClientId == 0) return;

        ulong target = currentTargetClientId;
        localState = LocalState.WaitingResponse;

        if (challengeUI != null)
            challengeUI.SetActive(false);

        SendChallengeServerRpc(target);
    }

    [ServerRpc]
    private void SendChallengeServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        
        
        ulong challengerId = rpcParams.Receive.SenderClientId;

        //No mandar retos si alguno ya está ocupado
        if (DuelArenaManager.Instance != null && (DuelArenaManager.Instance.IsPlayerBusy(challengerId) ||DuelArenaManager.Instance.IsPlayerBusy(targetClientId)))
        {
            SendFailureToClient(challengerId, "Uno de los jugadores ya está en un duelo.");
            return;
        }

        if (challengerId == targetClientId)
        {
            SendFailureToClient(challengerId, "No puedes retarte a ti mismo.");
            return;
        }

        if (IsPlayerBusy(challengerId) || IsPlayerBusy(targetClientId))
        {
            SendFailureToClient(challengerId, "Uno de los jugadores ya tiene un reto pendiente o está en duelo.");
            return;
        }

        ChallengeSystem challengerSystem = GetChallengeSystem(challengerId);
        ChallengeSystem targetSystem = GetChallengeSystem(targetClientId);

        if (challengerSystem == null || targetSystem == null)
        {
            SendFailureToClient(challengerId, "No se encontró el sistema de reto en uno de los jugadores.");
            return;
        }

        string challengerName = GetPlayerNameByClientId(challengerId);
        string targetName = GetPlayerNameByClientId(targetClientId);

        pendingByChallenger[challengerId] = targetClientId;
        pendingByTarget[targetClientId] = challengerId;

        challengerSystem.ShowWaitingClientRpc(targetName, GetClientRpcParams(challengerId));
        targetSystem.ReceiveChallengeClientRpc(challengerId, challengerName, GetClientRpcParams(targetClientId));
    }

    [ClientRpc]
    private void ShowWaitingClientRpc(string targetName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        localState = LocalState.WaitingResponse;
        Debug.Log($"Esperando respuesta de {targetName}...");
    }

    [ClientRpc]
    private void ReceiveChallengeClientRpc(ulong challengerId, string challengerName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        currentTargetClientId = challengerId;
        localState = LocalState.IncomingRequest;

        rivalNameText.text = $"{challengerName} te ha retado";
        SetButtonLabels("Aceptar", "Rechazar");

        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();

        acceptButton.onClick.AddListener(AcceptIncomingChallenge);
        rejectButton.onClick.AddListener(RejectIncomingChallenge);

        challengeUI.SetActive(true);
    }

    private void AcceptIncomingChallenge()
    {
        ulong challenger = currentTargetClientId;
        if (challenger == 0) return;

        if (challengeUI != null)
            challengeUI.SetActive(false);

        RespondToChallengeServerRpc(challenger, true);
    }

    private void RejectIncomingChallenge()
    {
        ulong challenger = currentTargetClientId;
        if (challenger == 0) return;

        if (challengeUI != null)
            challengeUI.SetActive(false);

        RespondToChallengeServerRpc(challenger, false);
    }

    [ServerRpc]
    private void RespondToChallengeServerRpc(ulong challengerId, bool accepted, ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        if (!pendingByTarget.TryGetValue(responderId, out ulong registeredChallenger))
        {
            Debug.LogWarning("No había un reto pendiente para este jugador.");
            return;
        }

        if (registeredChallenger != challengerId)
        {
            Debug.LogWarning("El reto no coincide con el challenger esperado.");
            return;
        }

        pendingByTarget.Remove(responderId);
        pendingByChallenger.Remove(challengerId);

        ChallengeSystem challengerSystem = GetChallengeSystem(challengerId);
        ChallengeSystem responderSystem = GetChallengeSystem(responderId);

        if (challengerSystem == null || responderSystem == null)
        {
            Debug.LogWarning("No se encontró ChallengeSystem en alguno de los jugadores.");
            return;
        }

        string responderName = GetPlayerNameByClientId(responderId);
        string challengerName = GetPlayerNameByClientId(challengerId);

        /*if (!accepted)
         {
             challengerSystem.NotifyChallengeRejectedClientRpc(responderName, GetClientRpcParams(challengerId));
             responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
             return;
         }*/
/*
if (!accepted)
        {
            challengerSystem.NotifyChallengeRejectedClientRpc(responderName, GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        if (DuelArenaManager.Instance == null)
        {
            challengerSystem.NotifyGenericMessageClientRpc("No existe DuelArenaManager en la escena.", GetClientRpcParams(challengerId));
            responderSystem.NotifyGenericMessageClientRpc("No existe DuelArenaManager en la escena.", GetClientRpcParams(responderId));

            challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        bool started = DuelArenaManager.Instance.TryStartDuel(challengerId, responderId, out int duelId);

        if (!started)
        {
            challengerSystem.NotifyGenericMessageClientRpc("No se pudo iniciar el duelo.", GetClientRpcParams(challengerId));
            responderSystem.NotifyGenericMessageClientRpc("No se pudo iniciar el duelo.", GetClientRpcParams(responderId));

            challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        challengerSystem.NotifyChallengeAcceptedClientRpc(responderName, GetClientRpcParams(challengerId));
        responderSystem.NotifyChallengeAcceptedClientRpc(challengerName, GetClientRpcParams(responderId));

        challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
        responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));

        /*if (DuelArenaManager.Instance == null)
        {
            challengerSystem.NotifyGenericMessageClientRpc("No existe DuelArenaManager en la escena.", GetClientRpcParams(challengerId));
            responderSystem.NotifyGenericMessageClientRpc("No existe DuelArenaManager en la escena.", GetClientRpcParams(responderId));
            challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        }

        bool started = DuelArenaManager.Instance.TryStartDuel(challengerId, responderId);

        if (!started)
        {
            challengerSystem.NotifyGenericMessageClientRpc("No se pudo iniciar el duelo (no hay arena disponible).", GetClientRpcParams(challengerId));
            responderSystem.NotifyGenericMessageClientRpc("No se pudo iniciar el duelo (no hay arena disponible).", GetClientRpcParams(responderId));
            challengerSystem.ResetLocalStateClientRpc(GetClientRpcParams(challengerId));
            responderSystem.ResetLocalStateClientRpc(GetClientRpcParams(responderId));
            return;
        //}

        duelLockedPlayers.Add(challengerId);
        duelLockedPlayers.Add(responderId);

        challengerSystem.NotifyChallengeAcceptedClientRpc(responderName, GetClientRpcParams(challengerId));
        responderSystem.NotifyChallengeAcceptedClientRpc(challengerName, GetClientRpcParams(responderId));
    }

    [ClientRpc]
    private void NotifyChallengeAcceptedClientRpc(string otherPlayerName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        localState = LocalState.InDuel;
        Debug.Log($"{otherPlayerName} aceptó el reto.");
    }

    [ClientRpc]
    private void NotifyChallengeRejectedClientRpc(string otherPlayerName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        Debug.Log($"{otherPlayerName} rechazó el reto.");
        HideUI();
    }

    [ClientRpc]
    private void NotifyGenericMessageClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        Debug.Log(message);
    }

    [ClientRpc]
    private void ResetLocalStateClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        HideUI();
    }

    private void SendFailureToClient(ulong clientId, string message)
    {
        ChallengeSystem system = GetChallengeSystem(clientId);
        if (system == null) return;

        system.NotifyGenericMessageClientRpc(message, GetClientRpcParams(clientId));
        system.ResetLocalStateClientRpc(GetClientRpcParams(clientId));
    }

    private static bool IsPlayerBusy(ulong clientId)
    {
        return duelLockedPlayers.Contains(clientId)
               || pendingByChallenger.ContainsKey(clientId)
               || pendingByTarget.ContainsKey(clientId);
    }

    private static ChallengeSystem GetChallengeSystem(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            return null;

        if (clientData.PlayerObject == null)
            return null;

        return clientData.PlayerObject.GetComponent<ChallengeSystem>();
    }


    private static string GetPlayerNameByClientId(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return $"Player_{clientId}";

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            return $"Player_{clientId}";

        if (clientData.PlayerObject == null)
            return $"Player_{clientId}";

        PlayerNameDisplay display = clientData.PlayerObject.GetComponentInChildren<PlayerNameDisplay>(true);
        if (display == null)
            return $"Player_{clientId}";

        string finalName = display.GetPlayerName();
        if (string.IsNullOrWhiteSpace(finalName))
            finalName = $"Player_{clientId}";

        return finalName;
    }
    private static ClientRpcParams GetClientRpcParams(ulong targetClientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };
    }

    // Llamar esto cuando termine el combate
    public static void ReleasePlayers(ulong playerA, ulong playerB)
    {
        duelLockedPlayers.Remove(playerA);
        duelLockedPlayers.Remove(playerB);
    }
}
*/