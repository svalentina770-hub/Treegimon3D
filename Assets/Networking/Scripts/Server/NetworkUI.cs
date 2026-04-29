using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string serverAddress = "127.0.0.1";

    [Header("Camera")]
    [SerializeField] private SmoothCameraFollow cameraFollow;
    [SerializeField] private float maxCameraSearchTime = 10f;

    [Header("Debug")]
    [SerializeField] private bool mostrarBotonesHostYServer = true;
    [SerializeField] private bool mostrarCampoIP = true;

    private string playerName = "";
    private string selectedPlantId = "aliso";
    private string selectedPlantLevel = "3";

    private void Start()
    {
        playerName = PlayerPrefs.GetString("PLAYER_NAME", "");
        serverAddress = PlayerPrefs.GetString("SERVER_IP", serverAddress);
        selectedPlantId = PlayerPrefs.GetString("SELECTED_PLANT_ID", selectedPlantId);
        selectedPlantLevel = PlayerPrefs.GetInt("SELECTED_PLANT_LEVEL", 3).ToString();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager.Singleton no existe.");
            return;
        }

        if (Application.isBatchMode && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Iniciando servidor dedicado automáticamente...");
            NetworkManager.Singleton.StartServer();
        }
    }

    private void OnGUI()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        if (Application.isBatchMode) return;
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            return;

        GUI.Label(new Rect(10, 10, 70, 20), "Nombre:");
        playerName = GUI.TextField(new Rect(80, 10, 180, 25), playerName);

        GUI.Label(new Rect(10, 45, 70, 20), "Planta:");
        selectedPlantId = GUI.TextField(new Rect(80, 45, 180, 25), selectedPlantId);

        GUI.Label(new Rect(10, 80, 70, 20), "Nivel:");
        selectedPlantLevel = GUI.TextField(new Rect(80, 80, 180, 25), selectedPlantLevel);

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

    private void StartClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No existe NetworkManager.Singleton.");
            return;
        }

        SavePlayerData();

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport en el NetworkManager.");
            return;
        }

        transport.SetConnectionData(serverAddress, port);
        NetworkManager.Singleton.StartClient();
        StartCoroutine(AssignCameraWhenPlayerExists());
    }

    private IEnumerator AssignCameraWhenPlayerExists()
    {
        if (cameraFollow == null)
        {
            cameraFollow = FindObjectOfType<SmoothCameraFollow>();
        }

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
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        if (string.IsNullOrWhiteSpace(selectedPlantId))
            selectedPlantId = "aliso";

        int parsedLevel = 3;
        int.TryParse(selectedPlantLevel, out parsedLevel);
        parsedLevel = Mathf.Max(1, parsedLevel);

        PlayerPrefs.SetString("PLAYER_NAME", playerName.Trim());
        PlayerPrefs.SetString("SERVER_IP", serverAddress.Trim());
        PlayerPrefs.SetString("SELECTED_PLANT_ID", selectedPlantId.Trim().ToLower());
        PlayerPrefs.SetInt("SELECTED_PLANT_LEVEL", parsedLevel);
        PlayerPrefs.Save();
    }
}