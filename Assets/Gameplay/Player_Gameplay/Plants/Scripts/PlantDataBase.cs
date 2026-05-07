using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Database", fileName = "PlantDatabase")]
public class PlantDataBase : ScriptableObject
{
    [Header("Especies base")]
    [SerializeField] private List<PlantSpeciesData> plants = new();

    [Header("Variantes / modelos 3D")]
    [Tooltip("Cada entrada representa un modelo concreto producido por estudiante. El modelKey debe coincidir con el nombre base del modelo sin _fase_1, _fase_2, etc.")]
    [SerializeField] private List<PlantModelVariantData> modelVariants = new();

    private Dictionary<string, PlantSpeciesData> speciesById;
    private Dictionary<string, PlantModelVariantData> variantsByModelKey;
    private Dictionary<string, List<PlantModelVariantData>> variantsBySpeciesId;

    private void OnEnable()
    {
        ClearCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClearCache();
    }
#endif

    private void ClearCache()
    {
        speciesById = null;
        variantsByModelKey = null;
        variantsBySpeciesId = null;
    }

    private void BuildCache()
    {
        if (speciesById != null && variantsByModelKey != null && variantsBySpeciesId != null)
            return;

        speciesById = new Dictionary<string, PlantSpeciesData>();
        variantsByModelKey = new Dictionary<string, PlantModelVariantData>();
        variantsBySpeciesId = new Dictionary<string, List<PlantModelVariantData>>();

        RegisterSpecies();
        RegisterVariants();
    }

    private void RegisterSpecies()
    {
        if (plants == null)
            return;

        foreach (PlantSpeciesData plant in plants)
        {
            if (plant == null || string.IsNullOrWhiteSpace(plant.plantId))
                continue;

            string key = NormalizeKey(plant.plantId);

            if (speciesById.ContainsKey(key))
            {
                Debug.LogWarning($"PlantDataBase: especie duplicada con id '{plant.plantId}'. Se conservará la última referencia registrada.");
                speciesById[key] = plant;
                continue;
            }

            speciesById.Add(key, plant);
        }
    }

    private void RegisterVariants()
    {
        if (modelVariants == null)
            return;

        foreach (PlantModelVariantData variant in modelVariants)
        {
            if (variant == null)
                continue;

            if (!string.IsNullOrWhiteSpace(variant.modelKey))
            {
                string modelKey = NormalizeKey(variant.modelKey);

                if (variantsByModelKey.ContainsKey(modelKey))
                {
                    Debug.LogWarning($"PlantDataBase: variante duplicada con modelKey '{variant.modelKey}'. Se conservará la última referencia registrada.");
                    variantsByModelKey[modelKey] = variant;
                }
                else
                {
                    variantsByModelKey.Add(modelKey, variant);
                }
            }

            if (string.IsNullOrWhiteSpace(variant.baseSpeciesId))
                continue;

            string speciesKey = NormalizeKey(variant.baseSpeciesId);

            if (!variantsBySpeciesId.TryGetValue(speciesKey, out List<PlantModelVariantData> variants))
            {
                variants = new List<PlantModelVariantData>();
                variantsBySpeciesId.Add(speciesKey, variants);
            }

            variants.Add(variant);
        }
    }

    public PlantSpeciesData GetById(string plantId)
    {
        BuildCache();

        if (string.IsNullOrWhiteSpace(plantId))
            return null;

        speciesById.TryGetValue(NormalizeKey(plantId), out PlantSpeciesData result);
        return result;
    }

    public bool TryGetById(string plantId, out PlantSpeciesData result)
    {
        result = GetById(plantId);
        return result != null;
    }

    public PlantModelVariantData GetVariantByModelKey(string modelKey)
    {
        BuildCache();

        if (string.IsNullOrWhiteSpace(modelKey))
            return null;

        variantsByModelKey.TryGetValue(NormalizeKey(modelKey), out PlantModelVariantData result);
        return result;
    }

    public bool TryGetVariantByModelKey(string modelKey, out PlantModelVariantData result)
    {
        result = GetVariantByModelKey(modelKey);
        return result != null;
    }

    public PlantSpeciesData GetSpeciesByModelKey(string modelKey)
    {
        PlantModelVariantData variant = GetVariantByModelKey(modelKey);

        if (variant == null)
            return null;

        return GetById(variant.baseSpeciesId);
    }

    public List<PlantModelVariantData> GetVariantsBySpeciesId(string speciesId)
    {
        BuildCache();

        if (string.IsNullOrWhiteSpace(speciesId))
            return new List<PlantModelVariantData>();

        if (variantsBySpeciesId.TryGetValue(NormalizeKey(speciesId), out List<PlantModelVariantData> variants))
            return new List<PlantModelVariantData>(variants);

        return new List<PlantModelVariantData>();
    }

    public PlantModelVariantData FindVariant(string speciesNameOrId, string studentName)
    {
        BuildCache();

        string speciesKey = NormalizeKey(speciesNameOrId);
        string studentKey = NormalizeKey(studentName);

        if (string.IsNullOrWhiteSpace(speciesKey) || string.IsNullOrWhiteSpace(studentKey))
            return null;

        foreach (PlantModelVariantData variant in modelVariants)
        {
            if (variant == null)
                continue;

            bool speciesMatches =
                NormalizeKey(variant.baseSpeciesId) == speciesKey ||
                NormalizeKey(variant.speciesDisplayName) == speciesKey;

            bool studentMatches = NormalizeKey(variant.studentName) == studentKey;

            if (speciesMatches && studentMatches)
                return variant;
        }

        return null;
    }

    public PlantSpeciesData GetSpeciesFromUserPlant(string baseSpeciesId, string fallbackPlantId)
    {
        PlantSpeciesData species = GetById(baseSpeciesId);

        if (species != null)
            return species;

        return GetById(fallbackPlantId);
    }

    public IReadOnlyList<PlantSpeciesData> GetAllSpecies()
    {
        return plants;
    }

    public IReadOnlyList<PlantModelVariantData> GetAllVariants()
    {
        return modelVariants;
    }

    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (character == '_' || character == '-' || character == '.')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        string result = builder.ToString().Normalize(NormalizationForm.FormC);

        while (result.Contains("  "))
            result = result.Replace("  ", " ");

        return result.Trim();
    }
}

[Serializable]
public class PlantModelVariantData
{
    [Header("Identificación")]
    [Tooltip("Nombre base del modelo sin sufijo de fase. Ejemplo: Aliso_Juan_Pablo_Yalta_Badillo")]
    public string modelKey;

    [Tooltip("ID de especie base. Debe coincidir con PlantSpeciesData.plantId. Ejemplo: aliso")]
    public string baseSpeciesId;

    [Tooltip("Nombre visible de la especie. Ejemplo: Aliso")]
    public string speciesDisplayName;

    public int subspeciesId = 1;
    public string instanceId;
    public string studentName;

    [Header("Rutas Resources por fase")]
    [Tooltip("Ruta en Resources sin extensión. Ejemplo: Modelos_3D/fase1/Aliso_Juan_Pablo_Yalta_Badillo_fase_1")]
    public string fase1ResourcePath;

    public string fase2ResourcePath;
    public string fase3ResourcePath;
    public string fase4ResourcePath;

    public string GetResourcePathForPhase(string phase)
    {
        string normalizedPhase = PlantDataBase.NormalizeKey(phase).Replace(" ", string.Empty);

        switch (normalizedPhase)
        {
            case "fase1":
            case "semilla":
                return fase1ResourcePath;

            case "fase2":
            case "arbusto":
                return fase2ResourcePath;

            case "fase3":
            case "arbol":
            case "árbol":
                return fase3ResourcePath;

            case "fase4":
            case "ent":
                return fase4ResourcePath;

            default:
                return fase4ResourcePath;
        }
    }
}