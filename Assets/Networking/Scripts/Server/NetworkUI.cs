using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string serverAddress = "127.0.0.1";

    [Header("Dedicated Server")]
    [SerializeField] private bool autoStartDedicatedServerInBatchMode = true;
    [SerializeField] private string dedicatedServerBindAddress = "0.0.0.0";

    [Header("WebGL Client")]
    [SerializeField] private bool autoConnectClientOnWebGL = true;
    [SerializeField] private bool hideGuiOnWebGL = true;
    [SerializeField] private string fixedWebGLServerAddress = "142.93.60.198";
    [SerializeField] private float webGLAutoConnectDelay = 1f;

    [Header("Data User")]
    [SerializeField] private UserDataSourceMode dataSourceMode = UserDataSourceMode.Auto;
    [SerializeField] private string resourcesUserDataPath = "Data/Data_user";
    [SerializeField] private string androidUserDataPath = "/storage/emulated/0/Documents/IMAGINATIO/Data_user.tree";
    [SerializeField] private string webglLocalStorageKey = "imaginatio_tree_data";

    [Header("Camera")]
    [SerializeField] private SmoothCameraFollow cameraFollow;
    [SerializeField] private float maxCameraSearchTime = 10f;

    [Header("Debug")]
    [SerializeField] private bool mostrarBotonesHostYServer = true;
    [SerializeField] private bool mostrarCampoIP = true;

    private string playerName = "";
    private string selectedPlantId = "aliso";
    private string selectedPlantLevel = "1";
    private string selectedPlantInstanceId = "";
    private bool callbacksRegistered;
    private bool clientStartRequested;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetLocalStorageItem(string key);
#endif

    public enum UserDataSourceMode
    {
        Auto,
        ResourcesTextAsset,
        AndroidTreeFile,
        WebGLLocalStorage,
        PlayerPrefsOnly
    }

    private void Start()
    {
        RegisterNetworkCallbacks();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager.Singleton no existe.");
            return;
        }

        if (Application.isBatchMode && autoStartDedicatedServerInBatchMode)
        {
            StartDedicatedServerIfNeeded();
            return;
        }

        LoadNetworkDefaults();
        LoadPlayerDataFromTreeOrPrefs();

#if UNITY_WEBGL && !UNITY_EDITOR
        if (autoConnectClientOnWebGL)
        {
            serverAddress = fixedWebGLServerAddress;
            StartCoroutine(CoAutoConnectWebGLClient());
        }
#endif
    }

    private void OnDestroy()
    {
        UnregisterNetworkCallbacks();
    }

    private void RegisterNetworkCallbacks()
    {
        if (callbacksRegistered || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        callbacksRegistered = true;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (!callbacksRegistered || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        callbacksRegistered = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"NETCODE: Cliente conectado. ClientId={clientId}. LocalClientId={NetworkManager.Singleton.LocalClientId}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        string disconnectReason = string.Empty;

        if (NetworkManager.Singleton != null)
            disconnectReason = NetworkManager.Singleton.DisconnectReason;

        Debug.LogWarning(
            string.IsNullOrWhiteSpace(disconnectReason)
                ? $"NETCODE: Cliente desconectado. ClientId={clientId}. LocalClientId={NetworkManager.Singleton.LocalClientId}. Sin razón explícita de desconexión."
                : $"NETCODE: Cliente desconectado. ClientId={clientId}. LocalClientId={NetworkManager.Singleton.LocalClientId}. Razón: {disconnectReason}"
        );

        clientStartRequested = false;
    }

    private IEnumerator CoAutoConnectWebGLClient()
    {
        yield return new WaitForSeconds(webGLAutoConnectDelay);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkUI: No se puede autoconectar WebGL porque NetworkManager.Singleton es null.");
            yield break;
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || clientStartRequested)
            yield break;

        Debug.Log($"NetworkUI: autoconexión WebGL hacia {serverAddress}:{port}.");
        StartClient();
    }

    private void StartDedicatedServerIfNeeded()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkUI: No se puede iniciar servidor dedicado porque NetworkManager.Singleton es null.");
            return;
        }

        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            Debug.Log("NetworkUI: El servidor dedicado ya estaba iniciado o el NetworkManager ya está conectado.");
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(dedicatedServerBindAddress, port);
            Debug.Log($"NetworkUI: servidor dedicado configurado en {dedicatedServerBindAddress}:{port}.");
        }
        else
        {
            Debug.LogWarning("NetworkUI: No se encontró UnityTransport. Se intentará iniciar el servidor con la configuración actual del NetworkManager.");
        }

        bool started = NetworkManager.Singleton.StartServer();
        Debug.Log(started
            ? "NetworkUI: servidor dedicado iniciado correctamente."
            : "NetworkUI: no fue posible iniciar el servidor dedicado.");
    }

    private void OnGUI()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (hideGuiOnWebGL)
            return;
#endif
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        if (Application.isBatchMode) return;
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            return;

        GUI.Label(new Rect(10, 10, 70, 20), "Nombre:");

        GUI.enabled = false;
        playerName = GUI.TextField(new Rect(80, 10, 180, 25), playerName);

        GUI.Label(new Rect(10, 45, 70, 20), "Planta:");
        selectedPlantId = GUI.TextField(new Rect(80, 45, 180, 25), selectedPlantId);

        GUI.Label(new Rect(10, 80, 70, 20), "Nivel:");
        selectedPlantLevel = GUI.TextField(new Rect(80, 80, 180, 25), selectedPlantLevel);
        GUI.enabled = true;

        if (mostrarCampoIP)
        {
            GUI.Label(new Rect(10, 115, 70, 20), "IP:");
            serverAddress = GUI.TextField(new Rect(80, 115, 180, 25), serverAddress);
        }

        float y = mostrarCampoIP ? 155 : 115;

        if (mostrarBotonesHostYServer)
        {
            if (GUI.Button(new Rect(10, y, 80, 30), "Host"))
            {
                SavePlayerData();
                NetworkManager.Singleton.StartHost();
                StartCoroutine(AssignCameraWhenPlayerExists());
            }

            if (GUI.Button(new Rect(100, y, 80, 30), "Server"))
            {
                SavePlayerData();
                NetworkManager.Singleton.StartServer();
            }

            if (GUI.Button(new Rect(190, y, 80, 30), "Client"))
            {
                StartClient();
            }
        }
        else
        {
            if (GUI.Button(new Rect(10, y, 100, 30), "Client"))
            {
                StartClient();
            }
        }
    }

    private void LoadNetworkDefaults()
    {
        serverAddress = PlayerPrefs.GetString("SERVER_IP", serverAddress);
        selectedPlantId = PlayerPrefs.GetString("SELECTED_PLANT_ID", selectedPlantId);
        selectedPlantLevel = PlayerPrefs.GetInt("SELECTED_PLANT_LEVEL", 1).ToString();
        selectedPlantInstanceId = PlayerPrefs.GetString("SELECTED_PLANT_INSTANCE_ID", "");
        playerName = PlayerPrefs.GetString("PLAYER_NAME", "");
    }

    private void LoadPlayerDataFromTreeOrPrefs()
    {
        string json = LoadUserDataJson();

        if (string.IsNullOrWhiteSpace(json))
        {
            ApplySafeFallbackValues();
            SavePlayerData();
            return;
        }

        UserTreeData userData = null;

        try
        {
            userData = JsonUtility.FromJson<UserTreeData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"NetworkUI: No se pudo leer el archivo de usuario. Error: {exception.Message}");
        }

        if (userData == null)
        {
            ApplySafeFallbackValues();
            SavePlayerData();
            return;
        }

        if (userData.usuario != null && !string.IsNullOrWhiteSpace(userData.usuario.nombre))
            playerName = userData.usuario.nombre;

        UserTreePlant selectedPlant = ResolveSelectedPlant(userData);

        if (selectedPlant != null)
        {
            selectedPlantId = FirstNonEmpty(
                selectedPlant.id_base_especie,
                selectedPlant.id
            );

            selectedPlantInstanceId = FirstNonEmpty(
                selectedPlant.id_instancia,
                selectedPlant.instance_id
            );

            int level = 1;

            if (selectedPlant.progreso != null && selectedPlant.progreso.nivel > 0)
                level = selectedPlant.progreso.nivel;

            selectedPlantLevel = Mathf.Max(1, level).ToString();
        }

        ApplySafeFallbackValues();
        SavePlayerData();
    }

    private UserTreePlant ResolveSelectedPlant(UserTreeData userData)
    {
        if (userData == null || userData.plantas == null || userData.plantas.Count == 0)
            return null;

        string savedInstanceId = PlayerPrefs.GetString("SELECTED_PLANT_INSTANCE_ID", "");
        string savedPlantId = PlayerPrefs.GetString("SELECTED_PLANT_ID", "");

        if (!string.IsNullOrWhiteSpace(savedInstanceId))
        {
            for (int i = 0; i < userData.plantas.Count; i++)
            {
                UserTreePlant plant = userData.plantas[i];
                if (plant == null) continue;

                string plantInstanceId = FirstNonEmpty(plant.id_instancia, plant.instance_id);

                if (string.Equals(plantInstanceId, savedInstanceId, StringComparison.OrdinalIgnoreCase))
                    return plant;
            }
        }

        if (!string.IsNullOrWhiteSpace(savedPlantId))
        {
            string normalizedSavedId = NormalizeKey(savedPlantId);

            for (int i = 0; i < userData.plantas.Count; i++)
            {
                UserTreePlant plant = userData.plantas[i];
                if (plant == null) continue;

                string plantId = NormalizeKey(FirstNonEmpty(plant.id_base_especie, plant.id));

                if (plantId == normalizedSavedId)
                    return plant;
            }
        }

        for (int i = 0; i < userData.plantas.Count; i++)
        {
            UserTreePlant plant = userData.plantas[i];

            if (plant != null && plant.uso != null && plant.uso.seleccionada)
                return plant;
        }

        for (int i = 0; i < userData.plantas.Count; i++)
        {
            UserTreePlant plant = userData.plantas[i];

            if (plant != null && plant.desbloqueada)
                return plant;
        }

        return userData.plantas[0];
    }

    private string LoadUserDataJson()
    {
        UserDataSourceMode mode = dataSourceMode == UserDataSourceMode.Auto ? GetAutoDataSourceMode() : dataSourceMode;

        switch (mode)
        {
            case UserDataSourceMode.AndroidTreeFile:
                return LoadFromAndroidFile();

            case UserDataSourceMode.WebGLLocalStorage:
                return LoadFromWebGLLocalStorage();

            case UserDataSourceMode.ResourcesTextAsset:
                return LoadFromResources();

            case UserDataSourceMode.PlayerPrefsOnly:
                return PlayerPrefs.GetString("imaginatio_tree_data_runtime_cache", "");

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
        TextAsset asset = Resources.Load<TextAsset>(resourcesUserDataPath);

        if (asset == null)
        {
            Debug.LogWarning($"NetworkUI: No se encontró archivo en Resources/{resourcesUserDataPath}.");
            return "";
        }

        return asset.text;
    }

    private string LoadFromAndroidFile()
    {
        try
        {
            if (!File.Exists(androidUserDataPath))
            {
                Debug.LogWarning($"NetworkUI: No existe archivo Android: {androidUserDataPath}");
                return "";
            }

            return File.ReadAllText(androidUserDataPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"NetworkUI: Error leyendo archivo Android: {exception.Message}");
            return "";
        }
    }

    private string LoadFromWebGLLocalStorage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return GetLocalStorageItem(webglLocalStorageKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"NetworkUI: Error leyendo LocalStorage: {exception.Message}");
            return "";
        }
#else
        return PlayerPrefs.GetString("imaginatio_tree_data_runtime_cache", "");
#endif
    }

    private void ApplySafeFallbackValues()
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        if (string.IsNullOrWhiteSpace(selectedPlantId))
            selectedPlantId = "aliso";

        if (string.IsNullOrWhiteSpace(selectedPlantLevel))
            selectedPlantLevel = "1";

        if (string.IsNullOrWhiteSpace(serverAddress))
            serverAddress = "127.0.0.1";
    }

    private void StartClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No existe NetworkManager.Singleton.");
            return;
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || clientStartRequested)
        {
            Debug.LogWarning("NetworkUI: StartClient ignorado porque NetworkManager ya está conectado o ya se solicitó conexión.");
            return;
        }

        clientStartRequested = true;
        SavePlayerData();

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport en el NetworkManager.");
            return;
        }

        transport.SetConnectionData(serverAddress, port);
        Debug.Log($"NetworkUI: intentando iniciar cliente hacia {serverAddress}:{port}.");

        bool started = NetworkManager.Singleton.StartClient();
        Debug.Log(started
            ? "NetworkUI: StartClient ejecutado correctamente. Esperando callback de conexión."
            : "NetworkUI: StartClient devolvió false. No se pudo iniciar el cliente.");

        if (!started)
        {
            clientStartRequested = false;
            return;
        }

        if (cameraFollow != null)
            StartCoroutine(AssignCameraWhenPlayerExists());
        else
            Debug.Log("NetworkUI: Cliente iniciado sin asignación automática de cámara.");
    }

    private IEnumerator AssignCameraWhenPlayerExists()
    {
        if (cameraFollow == null)
            cameraFollow = FindObjectOfType<SmoothCameraFollow>();

        if (cameraFollow == null)
        {
            Debug.LogWarning("NetworkUI: No se encontró un componente SmoothCameraFollow en la escena.");
            yield break;
        }

        float elapsedTime = 0f;

        while (GameObject.Find("Player") == null && elapsedTime < maxCameraSearchTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cameraFollow.FindPlayerTarget();
    }

    private void SavePlayerData()
    {
        ApplySafeFallbackValues();

        int parsedLevel = 1;
        int.TryParse(selectedPlantLevel, out parsedLevel);
        parsedLevel = Mathf.Max(1, parsedLevel);

        PlayerPrefs.SetString("PLAYER_NAME", playerName.Trim());
        PlayerPrefs.SetString("SERVER_IP", serverAddress.Trim());
        PlayerPrefs.SetString("SELECTED_PLANT_ID", NormalizeKey(selectedPlantId));
        PlayerPrefs.SetInt("SELECTED_PLANT_LEVEL", parsedLevel);

        if (!string.IsNullOrWhiteSpace(selectedPlantInstanceId))
            PlayerPrefs.SetString("SELECTED_PLANT_INSTANCE_ID", selectedPlantInstanceId);

        PlayerPrefs.Save();
    }

    private static string FirstNonEmpty(params string[] values)
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

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant();
    }

    [Serializable]
    private class UserTreeData
    {
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
        public UserTreePlantUso uso;
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
    private class UserTreePlantUso
    {
        public bool seleccionada;
        public bool en_combate;
    }
}