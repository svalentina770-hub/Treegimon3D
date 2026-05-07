using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ForceWebGLClientOnlyNetworkUI : MonoBehaviour
{
    [Header("Servidor dedicado")]
    [SerializeField] private string serverAddress = "142.93.60.198";
    [SerializeField] private ushort serverPort = 7777;

    [Header("Configuración")]
    [SerializeField] private float delayBeforeSetup = 0.25f;

    private void Start()
    {
        Invoke(nameof(ConfigureGeneratedNetworkUI), delayBeforeSetup);
    }

    private void ConfigureGeneratedNetworkUI()
    {
        ConfigureTransport();

        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        TMP_InputField[] inputFields = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            string buttonText = GetButtonText(button);

            if (buttonText == "Host")
            {
                button.gameObject.SetActive(false);
            }
            else if (buttonText == "Server")
            {
                button.gameObject.SetActive(false);
            }
            else if (buttonText == "Client")
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(StartClientDirectly);
            }
        }

        foreach (TMP_InputField input in inputFields)
        {
            string parentName = input.transform.parent != null 
                ? input.transform.parent.name.ToLower() 
                : "";

            string objectName = input.gameObject.name.ToLower();

            if (parentName.Contains("ip") || objectName.Contains("ip"))
            {
                input.text = serverAddress;
                input.interactable = false;
            }
            else if (input.text == "127.0.0.1")
            {
                input.text = serverAddress;
                input.interactable = false;
            }
        }

        Debug.Log("Network UI configurado solo como cliente hacia " + serverAddress + ":" + serverPort);
    }

    private string GetButtonText(Button button)
    {
        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>();

        if (tmpText != null)
            return tmpText.text.Trim();

        Text legacyText = button.GetComponentInChildren<Text>();

        if (legacyText != null)
            return legacyText.text.Trim();

        return button.gameObject.name.Trim();
    }

    private void ConfigureTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No existe NetworkManager.Singleton en la escena.");
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport en el mismo objeto del NetworkManager.");
            return;
        }

        transport.SetConnectionData(serverAddress, serverPort);

        Debug.Log("UnityTransport configurado con " + serverAddress + ":" + serverPort);
    }

    private void StartClientDirectly()
    {
        ConfigureTransport();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No existe NetworkManager.Singleton.");
            return;
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Ya hay una conexión activa.");
            return;
        }

        bool result = NetworkManager.Singleton.StartClient();

        Debug.Log("StartClient ejecutado hacia " + serverAddress + ":" + serverPort + ". Resultado: " + result);
    }
}