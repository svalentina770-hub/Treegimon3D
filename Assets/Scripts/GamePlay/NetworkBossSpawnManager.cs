using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkBossSpawnManager : MonoBehaviour
{
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private BossSpawnPoint[] spawnPoints;

    private readonly List<NetworkObject> spawnedBosses = new();
    private bool hasSpawned;

    private void Start()
    {
        if (spawnOnStart)
            StartCoroutine(CoSpawnWhenServerIsReady());
    }

    private IEnumerator CoSpawnWhenServerIsReady()
    {
        yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

        if (!NetworkManager.Singleton.IsServer)
            yield break;

        SpawnBosses();
    }

    [ContextMenu("Find Spawn Points In Scene")]
    private void FindSpawnPointsInScene()
    {
        spawnPoints = FindObjectsByType<BossSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    [ContextMenu("Spawn Bosses")]
    public void SpawnBosses()
    {
        if (hasSpawned)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("NetworkBossSpawnManager: solo el servidor puede spawnear bosses.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
            FindSpawnPointsInScene();

        foreach (BossSpawnPoint spawnPoint in spawnPoints)
        {
            SpawnBoss(spawnPoint);
        }

        hasSpawned = true;
    }

    private void SpawnBoss(BossSpawnPoint spawnPoint)
    {
        if (spawnPoint == null)
            return;

        GameObject prefab = spawnPoint.BossPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"NetworkBossSpawnManager: spawn point '{spawnPoint.name}' no tiene bossPrefab asignado.");
            return;
        }

        NetworkObject prefabNetworkObject = prefab.GetComponent<NetworkObject>();
        if (prefabNetworkObject == null)
        {
            Debug.LogError($"NetworkBossSpawnManager: el prefab '{prefab.name}' no tiene NetworkObject en la raíz.");
            return;
        }

        GameObject instance = Instantiate(prefab, spawnPoint.transform.position, spawnPoint.transform.rotation);
        instance.name = string.IsNullOrWhiteSpace(spawnPoint.BossId)
            ? prefab.name
            : spawnPoint.BossId;

        BossZoneController bossController = instance.GetComponentInChildren<BossZoneController>(true);
        if (bossController != null && spawnPoint.AssignedZone != null)
            bossController.AssignZone(spawnPoint.AssignedZone);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        networkObject.Spawn(true);
        spawnedBosses.Add(networkObject);

        Debug.Log($"NetworkBossSpawnManager: boss '{instance.name}' spawneado desde prefab '{prefab.name}'.");
    }
}
