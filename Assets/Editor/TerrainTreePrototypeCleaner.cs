#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TerrainTreePrototypeCleaner
{
    private const string MenuRoot = "Tools/Terrain/";

    [MenuItem(MenuRoot + "Report Missing Tree Prefabs")]
    public static void ReportMissingTreePrefabs()
    {
        int missingPrototypeCount = 0;

        foreach (TerrainData terrainData in FindTerrainDataAssets())
        {
            TreePrototype[] prototypes = terrainData.treePrototypes;
            if (prototypes == null || prototypes.Length == 0)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(terrainData);
            for (int index = 0; index < prototypes.Length; index++)
            {
                if (IsMissingPrototype(prototypes[index]))
                {
                    missingPrototypeCount++;
                    Debug.LogWarning(
                        $"Terrain '{path}' has a missing tree prefab at prototype index {index}.",
                        terrainData);
                }
            }
        }

        Debug.Log($"Terrain tree prefab report finished. Missing prototypes found: {missingPrototypeCount}.");
    }

    [MenuItem(MenuRoot + "Clean Missing Tree Prefabs")]
    public static void CleanMissingTreePrefabs()
    {
        int changedTerrainCount = 0;
        int removedPrototypeCount = 0;
        int removedInstanceCount = 0;

        foreach (TerrainData terrainData in FindTerrainDataAssets())
        {
            TreePrototype[] prototypes = terrainData.treePrototypes;
            if (prototypes == null || prototypes.Length == 0)
            {
                continue;
            }

            var indexMap = new Dictionary<int, int>();
            var validPrototypes = new List<TreePrototype>(prototypes.Length);
            string path = AssetDatabase.GetAssetPath(terrainData);

            for (int index = 0; index < prototypes.Length; index++)
            {
                TreePrototype prototype = prototypes[index];
                if (IsMissingPrototype(prototype))
                {
                    removedPrototypeCount++;
                    Debug.LogWarning(
                        $"Removing missing tree prefab from terrain '{path}' at prototype index {index}.",
                        terrainData);
                    continue;
                }

                indexMap[index] = validPrototypes.Count;
                validPrototypes.Add(prototype);
            }

            if (validPrototypes.Count == prototypes.Length)
            {
                continue;
            }

            TreeInstance[] treeInstances = terrainData.treeInstances;
            var validInstances = new List<TreeInstance>(treeInstances.Length);

            foreach (TreeInstance treeInstance in treeInstances)
            {
                if (!indexMap.TryGetValue(treeInstance.prototypeIndex, out int remappedIndex))
                {
                    removedInstanceCount++;
                    continue;
                }

                TreeInstance remappedInstance = treeInstance;
                remappedInstance.prototypeIndex = remappedIndex;
                validInstances.Add(remappedInstance);
            }

            Undo.RecordObject(terrainData, "Clean Missing Tree Prefabs");
            terrainData.treeInstances = validInstances.ToArray();
            terrainData.treePrototypes = validPrototypes.ToArray();
            EditorUtility.SetDirty(terrainData);
            changedTerrainCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Terrain tree prefab cleanup finished. " +
            $"Terrains changed: {changedTerrainCount}. " +
            $"Missing prototypes removed: {removedPrototypeCount}. " +
            $"Tree instances removed: {removedInstanceCount}.");
    }

    private static IEnumerable<TerrainData> FindTerrainDataAssets()
    {
        string[] terrainGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets" });
        foreach (string guid in terrainGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (terrainData != null)
            {
                yield return terrainData;
            }
        }
    }

    private static bool IsMissingPrototype(TreePrototype prototype)
    {
        return prototype == null || prototype.prefab == null;
    }
}
#endif
