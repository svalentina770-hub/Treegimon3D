using UnityEngine;
using Agones;

public class AgonesServerBridge : MonoBehaviour
{
    private AgonesSdk agones;

    void Start()
    {
        agones = new AgonesSdk();

        // 🔹 Marcar servidor como listo
        agones.Ready();
        Debug.Log("🟢 Agones Ready");
    }

    public void MarkAllocated()
    {
        agones.Allocate();
        Debug.Log("🟡 Allocated");
    }

    public void ShutdownServer()
    {
        agones.Shutdown();
        Debug.Log("🔴 Shutdown");
    }
}
