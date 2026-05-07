using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ForceClientOnlyNetworkUI : MonoBehaviour
{
    [Header("Servidor dedicado")]
    [SerializeField] private string serverAddress = "142.93.60.198";
    [SerializeField] private ushort serverPort = 7777;

    [Header("Control")]
    [SerializeField] private bool autoConnectOnStart = false;
    [SerializeField] private float firstDelay = 0.2f;
    [SerializeField] private float repeatTime = 0.2f;

    private bool clientButtonConfigured = false;

    private void Awake()
    {
        ConfigureTransport();
    }

    private void Start()
    {
        StartCoroutine(Co_ForceUI());

        if (autoConnectOnStart)
        {
            StartCoroutine(Co_AutoConnect());
        }
    }

    private IEnumerator Co_ForceUI()
    {
        yield return new WaitForSeconds(firstDelay);

        while (true)
        {
            ConfigureTransport();
            ForceInputFields();
            ForceButtons();

            yield return new WaitForSeconds(repeatTime);
        }
    }

    private IEnumerator Co_AutoConnect()
    {
        yield return new WaitForSeconds(1.0f);
        StartClientDirectly();
    }

    private void ConfigureTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("No existe NetworkManager.Singleton todavía.");
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("No se encontró UnityTransport en el NetworkManager.");
            return;
        }

        transport.SetConnectionData(serverAddress, serverPort);
    }

    private void ForceInputFields()
    {
        TMP_InputField[] tmpInputs = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (TMP_InputField input in tmpInputs)
        {
            string objectName = input.gameObject.name.ToLower();
            string parentName = input.transform.parent != null ? input.transform.parent.name.ToLower() : "";
            string currentText = input.text.Trim();

            bool looksLikeIpField =
                objectName.Contains("ip") ||
                parentName.Contains("ip") ||
                currentText == "127.0.0.1" ||
                currentText == "localhost" ||
                currentText == "142.93.60.198";

            if (looksLikeIpField)
            {
                input.text = serverAddress;
                input.interactable = false;
                input.readOnly = true;
            }
        }

        InputField[] legacyInputs = FindObjectsByType<InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (InputField input in legacyInputs)
        {
            string objectName = input.gameObject.name.ToLower();
            string parentName = input.transform.parent != null ? input.transform.parent.name.ToLower() : "";
            string currentText = input.text.Trim();

            bool looksLikeIpField =
                objectName.Contains("ip") ||
                parentName.Contains("ip") ||
                currentText == "127.0.0.1" ||
                currentText == "localhost" ||
                currentText == "142.93.60.198";

            if (looksLikeIpField)
            {
                input.text = serverAddress;
                input.interactable = false;
                input.readOnly = true;
            }
        }
    }

    private void ForceButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            string buttonLabel = GetButtonLabel(button).ToLower();
            string objectName = button.gameObject.name.ToLower();

            bool isHostButton =
                buttonLabel.Contains("host") ||
                objectName.Contains("host");

            bool isServerButton =
                buttonLabel.Contains("server") ||
                objectName.Contains("server");

            bool isClientButton =
                buttonLabel.Contains("client") ||
                objectName.Contains("client");

            if (isHostButton)
            {
                button.gameObject.SetActive(false);
            }
            else if (isServerButton)
            {
                button.gameObject.SetActive(false);
            }
            else if (isClientButton && !clientButtonConfigured)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(StartClientDirectly);
                clientButtonConfigured = true;

                Text legacyText = button.GetComponentInChildren<Text>(true);
                if (legacyText != null)
                    legacyText.text = "Conectar";

                TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
                if (tmpText != null)
                    tmpText.text = "Conectar";
            }
        }
    }

    private string GetButtonLabel(Button button)
    {
        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);

        if (tmpText != null)
            return tmpText.text.Trim();

        Text legacyText = button.GetComponentInChildren<Text>(true);

        if (legacyText != null)
            return legacyText.text.Trim();

        return button.gameObject.name.Trim();
    }

    public void StartClientDirectly()
    {
        ConfigureTransport();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No existe NetworkManager.Singleton.");
            return;
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Ya hay una sesión de red activa.");
            return;
        }

        bool result = NetworkManager.Singleton.StartClient();

        Debug.Log("Conectando como cliente a " + serverAddress + ":" + serverPort + " Resultado: " + result);
    }
}