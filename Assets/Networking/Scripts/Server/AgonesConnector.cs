using UnityEngine;
using Agones;

public class AgonesConnector : MonoBehaviour
{
    private AgonesSdk agones;

    private async void Start()
    {
        if (!Application.isBatchMode)
        {
            Debug.Log("AgonesConnector omitido: no está en batch mode.");
            return;
        }

        agones = GetComponent<AgonesSdk>();
        if (agones == null)
        {
            Debug.LogWarning("AgonesSdk no encontrado.");
            return;
        }

        bool connected = await agones.Connect();
        Debug.Log("AgonesConnector Connect => " + connected);
    }
}







/*using System.Collections;
using UnityEngine;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
public class AgonesConnector : MonoBehaviour
{
    private static readonly HttpClient client = new HttpClient();
    async void Start()
    {
        Debug.Log("Conectando a Agones...");
        await Task.Delay(2000);
        await Ready();
    }
    async Task Ready()
    {
        try
        {
            var response = await client.PostAsync(
            "http://localhost:2376/ready",
            new StringContent("")
            );
            Debug.Log("Agones Ready enviado: " + response.StatusCode);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error conectando a Agones: " + e.Message);
        }
    }
}

*/