#if UNITY_EDITOR
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public static class SceneBossNetworkObjectReporter
{
    [MenuItem("Tools/Networking/Report Scene Boss NetworkObjects")]
    public static void ReportSceneBossNetworkObjects()
    {
        BossZoneController[] bosses = Object.FindObjectsByType<BossZoneController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int count = 0;

        foreach (BossZoneController boss in bosses)
        {
            NetworkObject networkObject = boss.GetComponentInParent<NetworkObject>(true);
            if (networkObject == null)
                continue;

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(networkObject.gameObject);
            if (!string.IsNullOrEmpty(assetPath))
                continue;

            count++;
            Debug.LogWarning(
                $"Scene boss '{boss.name}' is using a scene-placed NetworkObject. " +
                "For separate client/server scenes, convert this boss to a registered NetworkPrefab and spawn it from NetworkBossSpawnManager.",
                networkObject);
        }

        Debug.Log($"Scene boss NetworkObject report finished. Scene-placed boss NetworkObjects found: {count}.");
    }
}
#endif
