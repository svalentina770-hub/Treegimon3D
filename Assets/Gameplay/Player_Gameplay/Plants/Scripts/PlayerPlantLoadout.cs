using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerPlantLoadout : NetworkBehaviour
{
    public enum UserDataSourceMode
    {
        Auto,
        PlayerPrefsOnly,
        ResourcesTextAsset,
        AndroidTreeFile,
        WebGLLocalStorage
    }

    [Header("Base de datos fija")]
    [SerializeField] private PlantDataBase plantDatabase;

    [Header("Carga de datos del usuario")]
    [SerializeField] private UserDataSourceMode dataSourceMode = UserDataSourceMode.Auto;
    [SerializeField] private string resourcesUserDataPath = "Data/Data_user";
    [SerializeField] private string androidUserDataPath = "/storage/emulated/0/Documents/IMAGINATIO/Data_user.tree";
    [Tooltip("Clave exacta usada por Plantagochi Web en localStorage. Ver UNITY_BRIDGE.md y unityBridge.ts: imaginatio_tree_data.")]
    [SerializeField] private string webglLocalStorageKey = "imaginatio_tree_data";

    [Header("Fallback / Debug")]
    [SerializeField] private string debugDefaultPlantId = "aliso";
    [SerializeField] private int debugDefaultLevel = 3;
    [SerializeField] private int debugDefaultHp = 1200;

    public NetworkVariable<FixedString64Bytes> equippedPlantId =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString512Bytes> equippedModelKey =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString64Bytes> equippedInstanceId =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString64Bytes> equippedPhase =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString128Bytes> equippedStudentName =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> plantLevel =
        new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentXP =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentHP =
        new(1200, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlantSpeciesData resolvedPlantData;
    private PlantModelVariantData resolvedVariantData;
    private PlayerPlantInstance localSelectedPlantInstance;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetLocalStorageItem(string key);

    [DllImport("__Internal")]
    private static extern void SetLocalStorageItem(string key, string value);
#endif

    public override void OnNetworkSpawn()
    {
        equippedPlantId.OnValueChanged += OnPlantChanged;
        equippedModelKey.OnValueChanged += OnModelChanged;
        equippedStudentName.OnValueChanged += OnModelChanged;

        ResolveCurrentPlant();

        if (IsOwner)
        {
            PlayerPlantInstance selectedPlant = LoadSelectedPlantInstance();
            SubmitLoadoutServerRpc(
                selectedPlant.EffectiveBaseSpeciesId,
                selectedPlant.ModelKey,
                selectedPlant.instanceId,
                selectedPlant.phase,
                selectedPlant.studentName,
                selectedPlant.level,
                selectedPlant.xp,
                selectedPlant.currentHp
            );
        }
    }

    public override void OnDestroy()
    {
        equippedPlantId.OnValueChanged -= OnPlantChanged;
        equippedModelKey.OnValueChanged -= OnModelChanged;
        equippedStudentName.OnValueChanged -= OnModelChanged;
        base.OnDestroy();
    }

    [ServerRpc]
    private void SubmitLoadoutServerRpc(
        string selectedPlantId,
        string selectedModelKey,
        string selectedInstanceId,
        string selectedPhase,
        string selectedStudentName,
        int selectedLevel,
        int selectedXp,
        int selectedHp)
    {
        if (string.IsNullOrWhiteSpace(selectedPlantId))
            selectedPlantId = debugDefaultPlantId;

        equippedPlantId.Value = selectedPlantId.Trim().ToLowerInvariant();
        equippedModelKey.Value = SafeFixedStringValue(selectedModelKey);
        equippedInstanceId.Value = SafeFixedStringValue(selectedInstanceId);
        equippedPhase.Value = SafeFixedStringValue(selectedPhase);
        equippedStudentName.Value = SafeFixedStringValue(selectedStudentName);
        plantLevel.Value = Mathf.Max(1, selectedLevel);
        currentXP.Value = Mathf.Max(0, selectedXp);
        currentHP.Value = Mathf.Max(0, selectedHp);
    }

    private string SafeFixedStringValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void OnPlantChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        ResolveCurrentPlant();
    }

    private void OnModelChanged(FixedString512Bytes oldValue, FixedString512Bytes newValue)
    {
        ResolveCurrentPlant();
    }

    private void OnModelChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
    {
        ResolveCurrentPlant();
    }

    private void ResolveCurrentPlant()
    {
        resolvedPlantData = null;
        resolvedVariantData = null;

        if (plantDatabase == null)
        {
            Debug.LogWarning("PlayerPlantLoadout: No hay PlantDatabase asignado.");
            return;
        }

        string id = equippedPlantId.Value.ToString();
        if (string.IsNullOrWhiteSpace(id))
            id = debugDefaultPlantId;

        resolvedPlantData = plantDatabase.GetById(id);

        string modelKey = equippedModelKey.Value.ToString();
        if (!string.IsNullOrWhiteSpace(modelKey))
            resolvedVariantData = plantDatabase.GetVariantByModelKey(modelKey);

        if (resolvedVariantData == null)
        {
            string studentName = equippedStudentName.Value.ToString();
            string speciesName = resolvedPlantData != null ? resolvedPlantData.displayName : id;
            resolvedVariantData = plantDatabase.FindVariant(speciesName, studentName);
        }

        if (resolvedPlantData == null && resolvedVariantData != null)
            resolvedPlantData = plantDatabase.GetById(resolvedVariantData.baseSpeciesId);

        if (resolvedPlantData == null)
            Debug.LogWarning($"PlayerPlantLoadout: No se encontró la planta con id '{id}'.");
    }

    private PlayerPlantInstance LoadSelectedPlantInstance()
    {
        PlayerPlantInstance fallback = BuildFallbackInstanceFromPlayerPrefs();
        string json = LoadUserDataJson();

        if (string.IsNullOrWhiteSpace(json))
        {
            localSelectedPlantInstance = fallback;
            return fallback;
        }

        try
        {
            UserTreeData userData = JsonUtility.FromJson<UserTreeData>(json);

            if (userData == null || userData.plantas == null || userData.plantas.Count == 0)
            {
                localSelectedPlantInstance = fallback;
                return fallback;
            }

            PlayerPlantInstance selected = FindSelectedPlant(userData.plantas);

            if (selected == null)
                selected = ConvertUserPlantToInstance(userData.plantas[0]);

            if (selected == null)
                selected = fallback;

            selected.ClampValues();
            localSelectedPlantInstance = selected;
            StoreSelectedPlantInPlayerPrefs(selected);
            return selected;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPlantLoadout: No fue posible leer Data_user/.tree. Se usará fallback. Error: {exception.Message}");
            localSelectedPlantInstance = fallback;
            return fallback;
        }
    }

    private PlayerPlantInstance FindSelectedPlant(List<UserTreePlant> plants)
    {
        string preferredInstanceId = PlayerPrefs.GetString("SelectedInstanceId", string.Empty);
        string preferredPlantId = PlayerPrefs.GetString("SelectedPlantId", PlayerPrefs.GetString("SELECTED_PLANT_ID", string.Empty));
        string preferredModelKey = PlayerPrefs.GetString("SelectedModelKey", string.Empty);

        for (int i = 0; i < plants.Count; i++)
        {
            PlayerPlantInstance instance = ConvertUserPlantToInstance(plants[i]);

            if (instance == null)
                continue;

            if (!string.IsNullOrWhiteSpace(preferredInstanceId) && instance.instanceId == preferredInstanceId)
                return instance;

            if (!string.IsNullOrWhiteSpace(preferredModelKey) && PlantDataBase.NormalizeKey(instance.ModelKey) == PlantDataBase.NormalizeKey(preferredModelKey))
                return instance;

            if (!string.IsNullOrWhiteSpace(preferredPlantId) && PlantDataBase.NormalizeKey(instance.EffectiveBaseSpeciesId) == PlantDataBase.NormalizeKey(preferredPlantId))
                return instance;

            if (instance.selected)
                return instance;
        }

        for (int i = 0; i < plants.Count; i++)
        {
            PlayerPlantInstance instance = ConvertUserPlantToInstance(plants[i]);

            if (instance != null && instance.IsUsableForCombat())
                return instance;
        }

        return null;
    }

    private PlayerPlantInstance ConvertUserPlantToInstance(UserTreePlant plant)
    {
        if (plant == null)
            return null;

        PlayerPlantInstance instance = new PlayerPlantInstance
        {
            plantId = FirstNonEmpty(plant.id, plant.id_base_especie),
            baseSpeciesId = FirstNonEmpty(plant.id_base_especie, plant.id),
            instanceId = FirstNonEmpty(plant.id_instancia, plant.instance_id),
            speciesName = FirstNonEmpty(plant.nombre_especie, plant.id_base_especie, plant.id),
            scientificName = plant.nombre_cientifico,
            studentName = FirstNonEmpty(plant.nombre_estudiante, plant.subid),
            unlocked = plant.desbloqueada,
            selected = plant.uso != null && plant.uso.seleccionada,
            inCombat = plant.uso != null && plant.uso.en_combate,
            phase = plant.estado != null ? FirstNonEmpty(plant.estado.fase, "semilla") : "semilla",
            healthState = plant.estado != null ? FirstNonEmpty(plant.estado.salud, "saludable") : "saludable",
            currentHp = plant.estado != null && plant.estado.hp_actual > 0 ? plant.estado.hp_actual : debugDefaultHp,
            level = plant.progreso != null && plant.progreso.nivel > 0 ? plant.progreso.nivel : debugDefaultLevel,
            xp = plant.progreso != null ? Mathf.Max(0, plant.progreso.xp) : 0,
            skin = plant.visual_estado != null ? FirstNonEmpty(plant.visual_estado.skin, "default") : "default",
            variation = plant.visual_estado != null ? FirstNonEmpty(plant.visual_estado.variacion, "normal") : "normal",
            waterApplied = plant.recursos_aplicados != null ? plant.recursos_aplicados.agua : 0,
            sunApplied = plant.recursos_aplicados != null ? plant.recursos_aplicados.sol : 0,
            compostApplied = plant.recursos_aplicados != null ? plant.recursos_aplicados.composta : 0
        };

        if (plant.id_subespecie > 0)
            instance.subspeciesId = plant.id_subespecie;

        return instance;
    }

    private PlayerPlantInstance BuildFallbackInstanceFromPlayerPrefs()
    {
        string selectedPlantId = PlayerPrefs.GetString("SelectedPlantId", PlayerPrefs.GetString("SELECTED_PLANT_ID", debugDefaultPlantId));
        string selectedModelKey = PlayerPrefs.GetString("SelectedModelKey", string.Empty);
        string selectedInstanceId = PlayerPrefs.GetString("SelectedInstanceId", string.Empty);
        string selectedPhase = PlayerPrefs.GetString("SelectedPhase", "fase4");
        int selectedLevel = PlayerPrefs.GetInt("SelectedPlantLevel", PlayerPrefs.GetInt("SELECTED_PLANT_LEVEL", debugDefaultLevel));
        int selectedXp = PlayerPrefs.GetInt("SelectedPlantXP", 0);
        int selectedHp = PlayerPrefs.GetInt("SelectedPlantHP", debugDefaultHp);

        PlantModelVariantData variant = null;
        if (plantDatabase != null && !string.IsNullOrWhiteSpace(selectedModelKey))
            variant = plantDatabase.GetVariantByModelKey(selectedModelKey);

        string baseSpeciesId = selectedPlantId;
        string speciesName = selectedPlantId;
        string studentName = string.Empty;
        int subspeciesId = 1;

        if (variant != null)
        {
            baseSpeciesId = variant.baseSpeciesId;
            speciesName = variant.speciesDisplayName;
            studentName = variant.studentName;
            subspeciesId = variant.subspeciesId;
        }

        PlayerPlantInstance fallback = new PlayerPlantInstance
        {
            plantId = selectedPlantId,
            baseSpeciesId = baseSpeciesId,
            instanceId = selectedInstanceId,
            subspeciesId = subspeciesId,
            speciesName = speciesName,
            studentName = studentName,
            unlocked = true,
            selected = true,
            phase = selectedPhase,
            currentHp = selectedHp,
            level = selectedLevel,
            xp = selectedXp
        };

        fallback.ClampValues();
        return fallback;
    }

    public bool TryAddRewardPlant(PlantSpeciesData rewardSpecies, PlantModelVariantData rewardVariant, out string rewardPlantDisplayName)
    {
        rewardPlantDisplayName = string.Empty;

        if (rewardSpecies == null)
        {
            Debug.LogWarning("PlayerPlantLoadout: No se pudo agregar recompensa porque rewardSpecies es null.");
            return false;
        }

        string json = LoadUserDataJson();
        UserTreeData userData = null;

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                userData = JsonUtility.FromJson<UserTreeData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PlayerPlantLoadout: No se pudo parsear el archivo de usuario para agregar recompensa. Se creará una estructura nueva. Error: {exception.Message}");
            }
        }

        if (userData == null)
            userData = CreateEmptyUserTreeData();

        if (userData.plantas == null)
            userData.plantas = new List<UserTreePlant>();

        UserTreePlant rewardPlant = BuildRewardUserTreePlant(rewardSpecies, rewardVariant);
        userData.plantas.Add(rewardPlant);

        string updatedJson = JsonUtility.ToJson(userData, true);

        if (!SaveUserDataJson(updatedJson))
        {
            Debug.LogWarning("PlayerPlantLoadout: No fue posible guardar la planta recompensa en el archivo .tree/.json.");
            return false;
        }

        rewardPlantDisplayName = FirstNonEmpty(rewardPlant.nombre_especie, rewardSpecies.displayName, rewardSpecies.plantId);
        Debug.Log($"PlayerPlantLoadout: Planta recompensa agregada al usuario: {rewardPlantDisplayName} ({rewardPlant.id_instancia}).");
        return true;
    }

    private UserTreeData CreateEmptyUserTreeData()
    {
        return new UserTreeData
        {
            version = 2,
            usuario = new UserTreeUser
            {
                id = "local_user",
                nombre = "Usuario local",
                nivel = 1,
                xp = 0
            },
            plantas = new List<UserTreePlant>()
        };
    }

    private UserTreePlant BuildRewardUserTreePlant(PlantSpeciesData rewardSpecies, PlantModelVariantData rewardVariant)
    {
        string baseSpeciesId = rewardSpecies.plantId;
        string speciesName = FirstNonEmpty(rewardSpecies.displayName, baseSpeciesId);
        string studentName = string.Empty;
        string modelKey = string.Empty;
        int subspeciesId = 1;

        if (rewardVariant != null)
        {
            baseSpeciesId = FirstNonEmpty(rewardVariant.baseSpeciesId, baseSpeciesId);
            speciesName = FirstNonEmpty(rewardVariant.speciesDisplayName, speciesName);
            studentName = FirstNonEmpty(rewardVariant.studentName, rewardVariant.modelKey);
            modelKey = rewardVariant.modelKey;
            subspeciesId = rewardVariant.subspeciesId > 0 ? rewardVariant.subspeciesId : 1;
        }

        string normalizedSpeciesId = PlantDataBase.NormalizeKey(baseSpeciesId);
        string instanceId = $"{normalizedSpeciesId}_reward_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{UnityEngine.Random.Range(1000, 9999)}";
        int initialHp = Mathf.Max(1, rewardSpecies.baseHP);

        return new UserTreePlant
        {
            id = normalizedSpeciesId,
            instance_id = instanceId,
            subid = studentName,
            desbloqueada = true,

            nombre_especie = speciesName,
            nombre_cientifico = string.Empty,
            id_base_especie = normalizedSpeciesId,
            id_subespecie = subspeciesId,
            id_instancia = instanceId,
            nombre_estudiante = studentName,
            model_key = modelKey,

            estado = new UserTreePlantEstado
            {
                fase = "semilla",
                salud = "saludable",
                hp_actual = initialHp
            },

            progreso = new UserTreePlantProgreso
            {
                nivel = 1,
                xp = 0
            },

            visual_estado = new UserTreePlantVisualEstado
            {
                skin = "default",
                variacion = "normal"
            },

            uso = new UserTreePlantUso
            {
                seleccionada = false,
                en_combate = false
            },

            recursos_aplicados = new UserTreeAppliedResources
            {
                agua = 0,
                sol = 0,
                composta = 0
            }
        };
    }

    private bool SaveUserDataJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        UserDataSourceMode mode = dataSourceMode == UserDataSourceMode.Auto ? GetAutoDataSourceMode() : dataSourceMode;

        switch (mode)
        {
            case UserDataSourceMode.AndroidTreeFile:
                return SaveToAndroidFile(json);

            case UserDataSourceMode.WebGLLocalStorage:
                return SaveToWebGLLocalStorage(json);

            case UserDataSourceMode.ResourcesTextAsset:
                return SaveToResourcesFileInEditor(json);

            case UserDataSourceMode.PlayerPrefsOnly:
                return SaveToPlayerPrefsFallback(json);

            default:
                return SaveToResourcesFileInEditor(json);
        }
    }

    private bool SaveToAndroidFile(string json)
    {
        try
        {
            string directory = Path.GetDirectoryName(androidUserDataPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(androidUserDataPath, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPlantLoadout: Error escribiendo archivo Android '{androidUserDataPath}': {exception.Message}");
            return false;
        }
    }

    private bool SaveToWebGLLocalStorage(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SetLocalStorageItem(webglLocalStorageKey, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPlantLoadout: Error escribiendo LocalStorage key '{webglLocalStorageKey}': {exception.Message}");
            return false;
        }
#else
        return SaveToPlayerPrefsFallback(json);
#endif
    }

    private bool SaveToResourcesFileInEditor(string json)
    {
#if UNITY_EDITOR
        try
        {
            string relativePathWithoutExtension = $"Assets/Resources/{resourcesUserDataPath}";
            string treePath = relativePathWithoutExtension + ".tree";
            string jsonPath = relativePathWithoutExtension + ".json";
            string targetPath = File.Exists(treePath) ? treePath : jsonPath;

            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(targetPath, json);
            AssetDatabase.Refresh();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPlantLoadout: Error escribiendo archivo Resources '{resourcesUserDataPath}': {exception.Message}");
            return false;
        }
#else
        return SaveToPlayerPrefsFallback(json);
#endif
    }

    private bool SaveToPlayerPrefsFallback(string json)
    {
        PlayerPrefs.SetString("imaginatio_tree_data_runtime_cache", json);
        PlayerPrefs.Save();
        return true;
    }

    private string LoadUserDataJson()
    {
        UserDataSourceMode mode = dataSourceMode == UserDataSourceMode.Auto ? GetAutoDataSourceMode() : dataSourceMode;

        switch (mode)
        {
            case UserDataSourceMode.PlayerPrefsOnly:
                return string.Empty;

            case UserDataSourceMode.ResourcesTextAsset:
                return LoadFromResources();

            case UserDataSourceMode.AndroidTreeFile:
                return LoadFromAndroidFile();

            case UserDataSourceMode.WebGLLocalStorage:
                return LoadFromWebGLLocalStorage();

            default:
                return LoadFromResources();
        }
    }

    private UserDataSourceMode GetAutoDataSourceMode()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return UserDataSourceMode.WebGLLocalStorage;
#elif UNITY_ANDROID && !UNITY_EDITOR
        return UserDataSourceMode.AndroidTreeFile;
#else
        return UserDataSourceMode.ResourcesTextAsset;
#endif
    }

    private string LoadFromResources()
    {
        TextAsset textAsset = Resources.Load<TextAsset>(resourcesUserDataPath);

        if (textAsset == null)
        {
            Debug.LogWarning($"PlayerPlantLoadout: No se encontró Resources/{resourcesUserDataPath}.json o .tree. Se usará fallback.");
            return string.Empty;
        }

        return textAsset.text;
    }

    private string LoadFromAndroidFile()
    {
        try
        {
            if (!File.Exists(androidUserDataPath))
            {
                Debug.LogWarning($"PlayerPlantLoadout: No existe el archivo Android '{androidUserDataPath}'. Se usará fallback.");
                return string.Empty;
            }

            return File.ReadAllText(androidUserDataPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPlantLoadout: Error leyendo archivo Android '{androidUserDataPath}': {exception.Message}");
            return string.Empty;
        }
    }

    private string LoadFromWebGLLocalStorage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            string rawData = GetLocalStorageItem(webglLocalStorageKey);

            if (string.IsNullOrWhiteSpace(rawData))
                Debug.LogWarning($"PlayerPlantLoadout: LocalStorage no contiene datos en la clave '{webglLocalStorageKey}'. Se usará fallback.");

            return rawData;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerPlantLoadout: Error leyendo LocalStorage key '{webglLocalStorageKey}': {exception.Message}");
            return string.Empty;
        }
#else
        return string.Empty;
#endif
    }

    private void StoreSelectedPlantInPlayerPrefs(PlayerPlantInstance selected)
    {
        if (selected == null)
            return;

        PlayerPrefs.SetString("SelectedPlantId", selected.EffectiveBaseSpeciesId);
        PlayerPrefs.SetString("SelectedInstanceId", selected.instanceId ?? string.Empty);
        PlayerPrefs.SetString("SelectedModelKey", selected.ModelKey ?? string.Empty);
        PlayerPrefs.SetString("SelectedPhase", selected.GetResourcePhaseName());
        PlayerPrefs.SetInt("SelectedPlantLevel", selected.level);
        PlayerPrefs.SetInt("SelectedPlantXP", selected.xp);
        PlayerPrefs.SetInt("SelectedPlantHP", selected.currentHp);
        PlayerPrefs.Save();
    }

    private string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return string.Empty;
    }

    public PlantSpeciesData GetPlantData()
    {
        if (resolvedPlantData == null)
            ResolveCurrentPlant();

        return resolvedPlantData;
    }

    public PlantModelVariantData GetVariantData()
    {
        if (resolvedVariantData == null)
            ResolveCurrentPlant();

        return resolvedVariantData;
    }

    public PlayerPlantInstance GetLocalSelectedPlantInstance()
    {
        return localSelectedPlantInstance;
    }

    public bool CanChallenge()
    {
        PlantSpeciesData plant = GetPlantData();
        return plant != null && plantLevel.Value >= plant.minLevelToPvP && currentHP.Value > 0;
    }

    public void AddXP(int amount)
    {
        if (!IsServer)
            return;

        currentXP.Value = Mathf.Max(0, currentXP.Value + amount);
    }

    public string GetPlantId()
    {
        return equippedPlantId.Value.ToString();
    }

    public string GetModelKey()
    {
        return equippedModelKey.Value.ToString();
    }

    public string GetPhase()
    {
        return equippedPhase.Value.ToString();
    }

    public int GetCurrentHp()
    {
        return currentHP.Value;
    }

    [Serializable]
    private class UserTreeData
    {
        public int version;
        public UserTreeUser usuario;
        public List<UserTreePlant> plantas;
    }

    [Serializable]
    private class UserTreeUser
    {
        public string id;
        public string nombre;
        public int nivel;
        public int xp;
    }

    [Serializable]
    private class UserTreePlant
    {
        public string id;
        public string instance_id;
        public string subid;
        public bool desbloqueada;

        public string nombre_especie;
        public string nombre_cientifico;
        public string id_base_especie;
        public int id_subespecie;
        public string id_instancia;
        public string nombre_estudiante;
        public string model_key;

        public UserTreePlantEstado estado;
        public UserTreePlantProgreso progreso;
        public UserTreePlantVisualEstado visual_estado;
        public UserTreePlantUso uso;
        public UserTreeAppliedResources recursos_aplicados;
    }

    [Serializable]
    private class UserTreePlantEstado
    {
        public string fase;
        public string salud;
        public int hp_actual;
    }

    [Serializable]
    private class UserTreePlantProgreso
    {
        public int nivel;
        public int xp;
    }

    [Serializable]
    private class UserTreePlantVisualEstado
    {
        public string skin;
        public string variacion;
    }

    [Serializable]
    private class UserTreePlantUso
    {
        public bool seleccionada;
        public bool en_combate;
    }

    [Serializable]
    private class UserTreeAppliedResources
    {
        public int agua;
        public int sol;
        public int composta;
    }
}