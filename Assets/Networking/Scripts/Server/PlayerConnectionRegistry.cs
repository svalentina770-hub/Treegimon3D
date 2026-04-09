using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;

public class PlayerConnectionRegistry : MonoBehaviour
{
    public static PlayerConnectionRegistry Instance;

    private readonly Dictionary<ulong, string> playerNames = new();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        //NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                               NetworkManager.ConnectionApprovalResponse response)
    {
        string playerName = "Player";

        if (request.Payload != null && request.Payload.Length > 0)
        {
            playerName = Encoding.UTF8.GetString(request.Payload).Trim();
        }

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = $"Player_{request.ClientNetworkId}";

        // Evitar nombres duplicados exactos
        if (playerNames.ContainsValue(playerName))
            playerName = $"{playerName}_{request.ClientNetworkId}";

        playerNames[request.ClientNetworkId] = playerName;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (playerNames.ContainsKey(clientId))
            playerNames.Remove(clientId);
    }

    public string GetPlayerName(ulong clientId)
    {
        if (playerNames.TryGetValue(clientId, out string value))
            return value;

        return $"Player_{clientId}";
    }
}