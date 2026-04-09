using Unity.Netcode;
using UnityEngine;
public class PlayerMovement: NetworkBehaviour
{
    public float speed = 4f;

    private void Update()
    {
        if (!IsOwner) return;
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0f, v);
        transform.Translate(move * speed * Time.deltaTime, Space.World);

    }
}
