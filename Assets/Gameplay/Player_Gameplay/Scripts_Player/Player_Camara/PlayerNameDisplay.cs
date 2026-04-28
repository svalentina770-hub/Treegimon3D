using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Collections;

public class PlayerNameDisplay : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Transform cameraTransform;

    public NetworkVariable<FixedString64Bytes> playerName =
        new NetworkVariable<FixedString64Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            string savedName = PlayerPrefs.GetString("PLAYER_NAME", "Player").Trim();

            if (string.IsNullOrWhiteSpace(savedName))
                savedName = "Player";

            SubmitNameServerRpc(savedName);
        }

        UpdateVisualName();
        playerName.OnValueChanged += OnNameChanged;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            transform.forward = cameraTransform.forward;
    }

    [ServerRpc]
    private void SubmitNameServerRpc(string newName, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
            newName = $"Player_{OwnerClientId}";

        playerName.Value = newName;
    }

    private void OnNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        UpdateVisualName();
    }

    private void UpdateVisualName()
    {
        string finalName = playerName.Value.ToString();

        if (string.IsNullOrWhiteSpace(finalName))
            finalName = $"Player_{OwnerClientId}";

        if (nameText != null)
            nameText.text = finalName;
    }

    public string GetPlayerName()
    {
        string finalName = playerName.Value.ToString();

        if (string.IsNullOrWhiteSpace(finalName))
            finalName = $"Player_{OwnerClientId}";

        return finalName;
    }

    public override void OnDestroy()
    {
        playerName.OnValueChanged -= OnNameChanged;
        base.OnDestroy();
    }
}