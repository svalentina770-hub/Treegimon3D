using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnArea : NetworkBehaviour
{
    public float checkRadius = 2f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GameObject area = GameObject.Find("SpawnArea");
        BoxCollider box = area.GetComponent<BoxCollider>();

        Vector3 center = box.bounds.center;
        Vector3 size = box.bounds.size;

        Vector3 spawnPos = center;

        int attempts = 0;
        bool found = false;

        while (!found && attempts < 20)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(center.x - size.x / 2, center.x + size.x / 2),
                center.y + 1f,
                Random.Range(center.z - size.z / 2, center.z + size.z / 2)
            );

            Collider[] hits = Physics.OverlapSphere(randomPos, checkRadius);

            bool occupied = false;

            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player")) // 🔥 SOLO jugadores
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
            {
                spawnPos = randomPos;
                found = true;
            }

            attempts++;
        }

        transform.position = spawnPos;
        
    }

}
