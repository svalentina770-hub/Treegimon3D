using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnSetter: NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if(IsServer)
        {
            GameObject spawn = GameObject.Find("SpawnPoint");
            if(spawn != null )
            {
                transform.position = spawn.transform.position;
            }
        }
    }
}
