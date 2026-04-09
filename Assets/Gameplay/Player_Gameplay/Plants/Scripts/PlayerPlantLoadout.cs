using UnityEngine;

public class PlayerPlantLoadout : MonoBehaviour
{
    [Header("Planta equipada actualmente")]
    public PlantSpeciesData equippedPlantData;

    [Header("Progreso actual")]
    public int plantLevel = 1;
    public int currentXP = 0;

    public bool CanChallenge()
    {
        if (equippedPlantData == null)
            return false;

        return plantLevel >= equippedPlantData.minLevelToPvP;
    }

    public string GetPlantName()
    {
        if (equippedPlantData == null)
            return "Sin planta";

        return equippedPlantData.displayName;
    }

    public PlantBiomeType GetBiomeType()
    {
        if (equippedPlantData == null)
            return PlantBiomeType.Neutro;

        return equippedPlantData.biomeType;
    }
}