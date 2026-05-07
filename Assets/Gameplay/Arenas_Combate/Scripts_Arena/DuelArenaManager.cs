using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;

public class DuelArenaManager : MonoBehaviour
{
    public static DuelArenaManager Instance { get; private set; }

    [Header("Prefab de arena")]
    [SerializeField] private CombatArenaInstance arenaPrefab;

    [Header("Organizaci�n en escena")]
    [SerializeField] private Transform arenaParent;
    [SerializeField] private Vector3 arenaBasePosition = new Vector3(0f, -500f, 0f);
    [SerializeField] private Vector3 arenaSpacing = new Vector3(0f, 0f, 300f);

    [Header("Retorno")]
    [SerializeField] private float returnSeparation = 4f;

    private int nextSessionId = 1;
    private int nextSlotIndex = 0;

    private readonly Queue<int> freeSlots = new();
    private readonly Dictionary<int, DuelSession> sessionsById = new();
    private readonly Dictionary<ulong, int> sessionIdByPlayer = new();

    private class DuelSession
    {
        public int sessionId;
        public int slotIndex;

        public ulong playerA;
        public ulong playerB;

        public bool isBossDuel;
        public ulong bossNetworkObjectId;

        public Vector3 bossReturnPos;
        public Quaternion bossReturnRot;

        public Vector3 returnPosA;
        public Quaternion returnRotA;

        public Vector3 returnPosB;
        public Quaternion returnRotB;

        public Vector3 arenaWorldPosition;
        public Quaternion arenaWorldRotation;

        public PlantBiomeType combatBiome;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;

        if (Instance == this)
            Instance = null;
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (sessionIdByPlayer.TryGetValue(clientId, out int duelId))
            EndDuel(duelId);
    }

    public bool IsPlayerBusy(ulong clientId)
    {
        return sessionIdByPlayer.ContainsKey(clientId);
    }

    public bool TryStartDuel(ulong playerA, ulong playerB, out int duelId)
    {
        duelId = 0;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return false;

        if (IsPlayerBusy(playerA) || IsPlayerBusy(playerB))
            return false;

        if (!TryGetPlayerObject(playerA, out NetworkObject objectA))
            return false;

        if (!TryGetPlayerObject(playerB, out NetworkObject objectB))
            return false;

        PlayerTeleport teleportA = objectA.GetComponent<PlayerTeleport>();
        PlayerTeleport teleportB = objectB.GetComponent<PlayerTeleport>();

        if (teleportA == null || teleportB == null || arenaPrefab == null)
        {
            Debug.LogWarning("Faltan referencias para iniciar el duelo.");
            return false;
        }

        if (arenaPrefab.AnchorA == null || arenaPrefab.AnchorB == null)
        {
            Debug.LogWarning("El prefab de arena no tiene AnchorA/AnchorB.");
            return false;
        }

        int slotIndex = AllocateSlot();
        Vector3 arenaWorldPosition = arenaBasePosition + arenaSpacing * slotIndex;
        Quaternion arenaWorldRotation = Quaternion.identity;

        duelId = nextSessionId++;

        Vector3 originalPosA = objectA.transform.position;
        Vector3 originalPosB = objectB.transform.position;

        Quaternion originalRotA = objectA.transform.rotation;
        Quaternion originalRotB = objectB.transform.rotation;

        Vector3 duelStartPoint = (originalPosA + originalPosB) * 0.5f;
        PlantBiomeType combatBiome = BiomeZoneRegistry.GetBiomeAtPosition(duelStartPoint);
        Debug.Log("Bioma detectado para el duelo: " + combatBiome);


        Vector3 center = duelStartPoint;
        Vector3 direction = originalPosB - originalPosA;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = objectA.transform.forward;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.right;

        direction.Normalize();

        Vector3 separatedReturnPosA = center - direction * (returnSeparation * 0.5f);
        Vector3 separatedReturnPosB = center + direction * (returnSeparation * 0.5f);

        Vector3 duelPosA = arenaWorldPosition + arenaPrefab.AnchorA.localPosition;
        Vector3 duelPosB = arenaWorldPosition + arenaPrefab.AnchorB.localPosition;

        Quaternion duelRotA = arenaWorldRotation * arenaPrefab.AnchorA.localRotation;
        Quaternion duelRotB = arenaWorldRotation * arenaPrefab.AnchorB.localRotation;

        string playerAName = GetPlayerDisplayName(playerA);
        string playerBName = GetPlayerDisplayName(playerB);

        DuelSession session = new DuelSession
        {
            sessionId = duelId,
            slotIndex = slotIndex,
            playerA = playerA,
            playerB = playerB,
            returnPosA = separatedReturnPosA,
            returnRotA = originalRotA,
            returnPosB = separatedReturnPosB,
            returnRotB = originalRotB,
            arenaWorldPosition = arenaWorldPosition,
            arenaWorldRotation = arenaWorldRotation,
            combatBiome = combatBiome
        };

        sessionsById[duelId] = session;
        sessionIdByPlayer[playerA] = duelId;
        sessionIdByPlayer[playerB] = duelId;

        teleportA.EnterDuelMode(
            duelPosA,
            duelRotA,
            arenaWorldPosition,
            arenaWorldRotation,
            playerAName,
            playerBName,
            (int)combatBiome
        );

        teleportB.EnterDuelMode(
            duelPosB,
            duelRotB,
            arenaWorldPosition,
            arenaWorldRotation,
            playerBName,
            playerAName,
            (int)combatBiome
        );

        DuelCombatManager.Instance?.StartCombatSession(duelId, playerA, playerB, combatBiome);

        Debug.Log($"Duelo iniciado. SessionId={duelId}, Slot={slotIndex}, Bioma={combatBiome}");
        return true;
    }

    public bool TryStartBossDuel(ulong playerId, NetworkObject bossNetworkObject, out int duelId)
    {
        duelId = 0;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return false;

        if (IsPlayerBusy(playerId))
            return false;

        if (bossNetworkObject == null)
            return false;

        if (!TryGetPlayerObject(playerId, out NetworkObject playerObject))
            return false;

        PlayerTeleport playerTeleport = playerObject.GetComponent<PlayerTeleport>();
        BossZoneController bossController = bossNetworkObject.GetComponentInChildren<BossZoneController>(true);

        if (playerTeleport == null || bossController == null || arenaPrefab == null)
        {
            Debug.LogWarning("Faltan referencias para iniciar el duelo contra boss.");
            return false;
        }

        if (arenaPrefab.AnchorA == null || arenaPrefab.AnchorB == null)
        {
            Debug.LogWarning("El prefab de arena no tiene AnchorA/AnchorB.");
            return false;
        }

        int slotIndex = AllocateSlot();
        Vector3 arenaWorldPosition = arenaBasePosition + arenaSpacing * slotIndex;
        Quaternion arenaWorldRotation = Quaternion.identity;

        duelId = nextSessionId++;

        Vector3 originalPlayerPos = playerObject.transform.position;
        Quaternion originalPlayerRot = playerObject.transform.rotation;

        Vector3 originalBossPos = bossNetworkObject.transform.position;
        Quaternion originalBossRot = bossNetworkObject.transform.rotation;

        Vector3 duelStartPoint = (originalPlayerPos + originalBossPos) * 0.5f;
        PlantBiomeType combatBiome = BiomeZoneRegistry.GetBiomeAtPosition(duelStartPoint);
        Debug.Log("Bioma detectado para duelo contra boss: " + combatBiome);

        Vector3 direction = originalBossPos - originalPlayerPos;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = playerObject.transform.forward;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.right;

        direction.Normalize();

        Vector3 separatedReturnPlayerPos = duelStartPoint - direction * (returnSeparation * 0.5f);

        Vector3 duelPosPlayer = arenaWorldPosition + arenaPrefab.AnchorA.localPosition;
        Vector3 duelPosBoss = arenaWorldPosition + arenaPrefab.AnchorB.localPosition;

        Quaternion duelRotPlayer = arenaWorldRotation * arenaPrefab.AnchorA.localRotation;
        Quaternion duelRotBoss = arenaWorldRotation * arenaPrefab.AnchorB.localRotation;

        string playerName = GetPlayerDisplayName(playerId);
        string bossName = bossController.BossDisplayName;

        DuelSession session = new DuelSession
        {
            sessionId = duelId,
            slotIndex = slotIndex,
            playerA = playerId,
            playerB = 0,
            isBossDuel = true,
            bossNetworkObjectId = bossNetworkObject.NetworkObjectId,
            returnPosA = separatedReturnPlayerPos,
            returnRotA = originalPlayerRot,
            returnPosB = originalBossPos,
            returnRotB = originalBossRot,
            bossReturnPos = originalBossPos,
            bossReturnRot = originalBossRot,
            arenaWorldPosition = arenaWorldPosition,
            arenaWorldRotation = arenaWorldRotation,
            combatBiome = combatBiome
        };

        sessionsById[duelId] = session;
        sessionIdByPlayer[playerId] = duelId;

        bossController.ForceStopForCombat();
        bossNetworkObject.transform.SetPositionAndRotation(duelPosBoss, duelRotBoss);

        playerTeleport.EnterDuelMode(
            duelPosPlayer,
            duelRotPlayer,
            arenaWorldPosition,
            arenaWorldRotation,
            playerName,
            bossName,
            (int)combatBiome
        );

        if (!TryStartBossCombatSession(duelId, playerId, bossNetworkObject, combatBiome))
            Debug.LogWarning("DuelCombatManager todavía no implementa StartBossCombatSession. Se teletransportó al jugador y al boss, pero no inició combate contra IA.");

        Debug.Log($"Duelo contra boss iniciado. SessionId={duelId}, Slot={slotIndex}, Boss={bossName}, Bioma={combatBiome}");
        return true;
    }

    public bool TryEndDuelByPlayer(ulong playerId)
    {
        if (!sessionIdByPlayer.TryGetValue(playerId, out int duelId))
            return false;

        return EndDuel(duelId);
    }

    public bool EndDuel(int duelId)
    {
        DuelCombatManager.Instance?.RemoveSessionSilently(duelId);

        if (!sessionsById.TryGetValue(duelId, out DuelSession session))
            return false;

        if (TryGetPlayerObject(session.playerA, out NetworkObject objectA))
        {
            PlayerTeleport teleportA = objectA.GetComponent<PlayerTeleport>();
            if (teleportA != null)
                teleportA.ExitDuelMode(session.returnPosA, session.returnRotA);
        }

        if (session.isBossDuel)
        {
            if (TryGetSpawnedNetworkObject(session.bossNetworkObjectId, out NetworkObject bossObject))
            {
                bossObject.transform.SetPositionAndRotation(session.bossReturnPos, session.bossReturnRot);

                BossZoneController bossController = bossObject.GetComponentInChildren<BossZoneController>(true);
                if (bossController != null)
                    bossController.ResumeMovement();
            }
        }
        else if (TryGetPlayerObject(session.playerB, out NetworkObject objectB))
        {
            PlayerTeleport teleportB = objectB.GetComponent<PlayerTeleport>();
            if (teleportB != null)
                teleportB.ExitDuelMode(session.returnPosB, session.returnRotB);
        }

        sessionsById.Remove(duelId);
        sessionIdByPlayer.Remove(session.playerA);

        if (!session.isBossDuel)
            sessionIdByPlayer.Remove(session.playerB);

        ReleaseSlot(session.slotIndex);

        Debug.Log($"Duelo finalizado. SessionId={duelId}");
        return true;
    }

    private bool TryGetPlayerObject(ulong clientId, out NetworkObject playerObject)
    {
        playerObject = null;

        if (NetworkManager.Singleton == null)
            return false;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            return false;

        playerObject = clientData.PlayerObject;
        return playerObject != null;
    }

    private bool TryGetSpawnedNetworkObject(ulong networkObjectId, out NetworkObject networkObject)
    {
        networkObject = null;

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            return false;

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out networkObject);
    }

    private bool TryStartBossCombatSession(int duelId, ulong playerId, NetworkObject bossNetworkObject, PlantBiomeType combatBiome)
    {
        if (DuelCombatManager.Instance == null || bossNetworkObject == null)
            return false;

        MethodInfo method = typeof(DuelCombatManager).GetMethod(
            "StartBossCombatSession",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(int), typeof(ulong), typeof(NetworkObject), typeof(PlantBiomeType) },
            null
        );

        if (method == null)
            return false;

        method.Invoke(DuelCombatManager.Instance, new object[] { duelId, playerId, bossNetworkObject, combatBiome });
        return true;
    }

    private string GetPlayerDisplayName(ulong clientId)
    {
        if (!TryGetPlayerObject(clientId, out NetworkObject playerObject))
            return $"Player_{clientId}";

        PlayerNameDisplay display = playerObject.GetComponentInChildren<PlayerNameDisplay>(true);
        if (display == null)
            return $"Player_{clientId}";

        string finalName = display.GetPlayerName();
        if (string.IsNullOrWhiteSpace(finalName))
            finalName = $"Player_{clientId}";

        return finalName;
    }

    private int AllocateSlot()
    {
        if (freeSlots.Count > 0)
            return freeSlots.Dequeue();

        return nextSlotIndex++;
    }

    private void ReleaseSlot(int slotIndex)
    {
        freeSlots.Enqueue(slotIndex);
    }
}