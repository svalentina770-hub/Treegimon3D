using System.Collections.Generic;
using UnityEngine;

public class ArenaBiomeVisualController : MonoBehaviour
{
    [System.Serializable]
    public class BiomeMaterialEntry
    {
        public PlantBiomeType biomeType;
        public Material material;
    }

    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private List<BiomeMaterialEntry> materials = new();

    public void ApplyBiome(PlantBiomeType biome)
    {
        Material selected = GetMaterialForBiome(biome);
        if (selected == null)
            return;

        foreach (Renderer r in targetRenderers)
        {
            if (r != null)
                r.material = selected;
        }
    }

    private Material GetMaterialForBiome(PlantBiomeType biome)
    {
        foreach (BiomeMaterialEntry entry in materials)
        {
            if (entry != null && entry.biomeType == biome)
                return entry.material;
        }

        return null;
    }
}