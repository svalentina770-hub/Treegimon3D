using UnityEngine;
using Unity.Netcode;
using Agones;

public class DedicatedServerBootstrap : MonoBehaviour
{
    private AgonesSdk agones;

    private async void Start()
    {
        Debug.Log("Bootstrap iniciado");

        if (!Application.isBatchMode)
        {
            Debug.Log("No está en batch mode. Agones se omite en editor/cliente.");
            return;
        }

        agones = GetComponent<AgonesSdk>();
        if (agones == null)
        {
            Debug.LogWarning("No se encontró AgonesSdk en este GameObject.");
            return;
        }

        bool connected = await agones.Connect();
        Debug.Log("Agones Connect => " + connected);

        if (!connected)
        {
            Debug.LogWarning("No se pudo conectar con Agones.");
            return;
        }

        bool ready = await agones.Ready();
        Debug.Log("Agones Ready => " + ready);
    }
}
















/*using UnityEngine;
using Unity.Netcode;
using Agones;

public class DedicatedServerBootstrap : MonoBehaviour
{
    private AgonesSdk agones;

    private async void Start()
    {
        Debug.Log("Bootstrap iniciado");

        if (!Application.isBatchMode)
        {
            Debug.Log("No está en batch mode");
            return;
        }

        agones = GetComponent<AgonesSdk>();
        if (agones == null)
        {
            Debug.LogError("Falta AgonesSdk en este GameObject");
            return;
        }

        bool connected = await agones.Connect();
        Debug.Log("Agones Connect => " + connected);

        if (!connected)
        {
            Debug.LogError("Connect falló");
            return;
        }

        agones.WatchGameServer(gs =>
        {
            Debug.Log("WatchGameServer => " + gs?.Status?.State);
        });

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No existe NetworkManager.Singleton");
            return;
        }

        bool started = NetworkManager.Singleton.StartServer();
        Debug.Log("StartServer => " + started);

        if (!started)
        {
            Debug.LogError("StartServer falló");
            return;
        }

        bool ready = await agones.Ready();
        Debug.Log("Agones Ready() => " + ready);

        var gs = await agones.GameServer();
        Debug.Log("GameServer state => " + gs?.Status?.State);
    }
}*/