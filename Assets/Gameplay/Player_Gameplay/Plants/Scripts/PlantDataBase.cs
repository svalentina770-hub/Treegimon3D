using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Database", fileName = "PlantDatabase")]
public class PlantDataBase : ScriptableObject
{
    [SerializeField] private List<PlantSpeciesData> plants = new();

    private Dictionary<string, PlantSpeciesData> cache;

    private void BuildCache()
    {
        if (cache != null) return;

        cache = new Dictionary<string, PlantSpeciesData>();

        foreach (PlantSpeciesData plant in plants)
        {
            if (plant == null || string.IsNullOrWhiteSpace(plant.plantId))
                continue;

            cache[plant.plantId.Trim().ToLower()] = plant;
        }
    }

    public PlantSpeciesData GetById(string plantId)
    {
        BuildCache();

        if (string.IsNullOrWhiteSpace(plantId))
            return null;

        cache.TryGetValue(plantId.Trim().ToLower(), out PlantSpeciesData result);
        return result;
    }
}