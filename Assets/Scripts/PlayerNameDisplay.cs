using UnityEngine;
using TMPro;

public class PlayerNameDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private string playerName = "Player1"; // ← editable por instancia

    private void Start()
    {
        SetName(playerName);
        cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        transform.forward = cameraTransform.forward;
    }

    public void SetName(string name)
    {
        nameText.text = name;
    }

    public string GetPlayerName()
    {
        return nameText.text;
    }
}