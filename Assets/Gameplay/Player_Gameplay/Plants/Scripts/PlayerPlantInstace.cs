using System;
using UnityEngine;

[Serializable]
public class PlayerPlantInstance
{
    [Header("Identificación")]
    public string plantId;
    public string baseSpeciesId;
    public string instanceId;
    public int subspeciesId = 1;
    public string speciesName;
    public string scientificName;
    public string studentName;

    [Header("Estado del jugador")]
    public bool unlocked = false;
    public bool selected = false;
    public bool inCombat = false;

    [Header("Crecimiento")]
    public string phase = "semilla";
    public string healthState = "saludable";
    public int currentHp = 1200;
    public int level = 1;
    public int xp = 0;

    [Header("Visual")]
    public string skin = "default";
    public string variation = "normal";

    [Header("Recursos aplicados")]
    public int waterApplied = 0;
    public int sunApplied = 0;
    public int compostApplied = 0;

    public string EffectiveBaseSpeciesId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(baseSpeciesId))
                return baseSpeciesId;

            return plantId;
        }
    }

    public string ModelKey
    {
        get
        {
            if (string.IsNullOrWhiteSpace(speciesName) || string.IsNullOrWhiteSpace(studentName))
                return string.Empty;

            return $"{speciesName}_{studentName}".Replace(" ", "_");
        }
    }

    public bool IsUsableForCombat()
    {
        return unlocked && currentHp > 0;
    }

    public bool IsSelectedAndUsableForCombat()
    {
        return selected && IsUsableForCombat();
    }

    public string GetResourcePhaseName()
    {
        string normalizedPhase = NormalizeSimple(phase);

        switch (normalizedPhase)
        {
            case "semilla":
            case "fase1":
                return "fase1";

            case "arbusto":
            case "fase2":
                return "fase2";

            case "arbol":
            case "árbol":
            case "fase3":
                return "fase3";

            case "ent":
            case "fase4":
                return "fase4";

            default:
                return "fase4";
        }
    }

    public void ClampValues()
    {
        if (level < 1)
            level = 1;

        if (xp < 0)
            xp = 0;

        if (currentHp < 0)
            currentHp = 0;

        if (subspeciesId < 1)
            subspeciesId = 1;
    }

    private static string NormalizeSimple(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant();
    }
}