using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(10, 10, 150, 30), "Host"))
            {
                NetworkManager.Singleton.StartHost();
            }

            if (GUI.Button(new Rect(10, 50, 150, 30), "Client"))
            {
                NetworkManager.Singleton.StartClient();
            }

            if (GUI.Button(new Rect(10, 90, 150, 30), "Server"))
            {
                NetworkManager.Singleton.StartServer();
            }
        }
    }

}
