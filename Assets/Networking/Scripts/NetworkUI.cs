using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string serverAddress = "127.0.0.1";

    [Header("Debug")]
    [SerializeField] private bool mostrarBotonesHostYServer = true;
    [SerializeField] private bool mostrarCampoIP = true;

    private string playerName = "";

    private void Start()
    {
        playerName = PlayerPrefs.GetString("PLAYER_NAME", "");
        serverAddress = PlayerPrefs.GetString("SERVER_IP", serverAddress);

        if (Application.isBatchMode && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Iniciando servidor dedicado automáticamente...");
            NetworkManager.Singleton.StartServer();
        }
    }

    private void OnGUI()
    {
        if (Application.isBatchMode) return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            return;

        GUI.Label(new Rect(10, 10, 70, 20), "Nombre:");
        playerName = GUI.TextField(new Rect(80, 10, 180, 25), playerName);

        if (mostrarCampoIP)
        {
            GUI.Label(new Rect(10, 45, 70, 20), "IP:");
            serverAddress = GUI.TextField(new Rect(80, 45, 180, 25), serverAddress);
        }

        float y = mostrarCampoIP ? 85 : 45;

        if (mostrarBotonesHostYServer)
        {
            if (GUI.Button(new Rect(10, y, 80, 30), "Host"))
            {
                SavePlayerData();
                NetworkManager.Singleton.StartHost();
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
        SavePlayerData();

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(serverAddress, port);

        NetworkManager.Singleton.StartClient();
    }

    private void SavePlayerData()
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        PlayerPrefs.SetString("PLAYER_NAME", playerName.Trim());
        PlayerPrefs.SetString("SERVER_IP", serverAddress.Trim());
        PlayerPrefs.Save();
    }
}