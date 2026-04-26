using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChallengeSystem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject challengeUI;
    [SerializeField] private TextMeshProUGUI rivalNameText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("TRIGGER ENTER detectado: " + other.gameObject.name);

        if (other.gameObject == gameObject)
        {
            Debug.Log("Era el mismo objeto, ignorado.");
            return;
        }

        PlayerNameDisplay rival = other.GetComponent<PlayerNameDisplay>();
        Debug.Log("PlayerNameDisplay en other: " + (rival != null ? "ENCONTRADO" : "NULL"));

        if (rival == null)
        {
            rival = other.GetComponentInParent<PlayerNameDisplay>();
            Debug.Log("PlayerNameDisplay en parent: " + (rival != null ? "ENCONTRADO" : "NULL"));
        }

        if (rival == null)
        {
            rival = other.GetComponentInChildren<PlayerNameDisplay>();
            Debug.Log("PlayerNameDisplay en children: " + (rival != null ? "ENCONTRADO" : "NULL"));
        }

        if (rival != null)
        {
            Debug.Log("Mostrando UI para: " + rival.GetPlayerName());
            rivalNameText.text = "Retar a: " + rival.GetPlayerName();
            acceptButton.onClick.RemoveAllListeners();
            rejectButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() => OnAccept(rival));
            rejectButton.onClick.AddListener(OnReject);
            challengeUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("TRIGGER EXIT: " + other.gameObject.name);

        // Buscar en todas las direcciones igual que en Enter
        PlayerNameDisplay rival = other.GetComponent<PlayerNameDisplay>();
        if (rival == null) rival = other.GetComponentInParent<PlayerNameDisplay>();
        if (rival == null) rival = other.GetComponentInChildren<PlayerNameDisplay>();

        if (rival != null)
        {
            Debug.Log("Ocultando UI");
            challengeUI.SetActive(false);
        }
    }

    private void OnAccept(PlayerNameDisplay rival)
    {
        Debug.Log("Batalla iniciada contra " + rival.GetPlayerName());
        challengeUI.SetActive(false);
    }

    private void OnReject()
    {
        Debug.Log("Reto rechazado.");
        challengeUI.SetActive(false);
    }
}