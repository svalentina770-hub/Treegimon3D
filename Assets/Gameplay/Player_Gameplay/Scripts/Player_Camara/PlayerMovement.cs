using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    // El joystick escribe aquí, el teclado también
    [HideInInspector] public Vector2 joystickInput;

    private Vector3 moveInput;


    void Update()
    {
        if (!IsOwner) return;
        // --- Input teclado ---
        Vector2 keyboardInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            float h = (Keyboard.current.dKey.isPressed ? 1 : 0)
                    - (Keyboard.current.aKey.isPressed ? 1 : 0);
            float v = (Keyboard.current.wKey.isPressed ? 1 : 0)
                    - (Keyboard.current.sKey.isPressed ? 1 : 0);
            keyboardInput = new Vector2(h, v);
        }

        // --- Combinar inputs (gana el que tenga magnitud) ---
        Vector2 rawInput = keyboardInput.magnitude > 0 ? keyboardInput : joystickInput;

        // --- Dirección isométrica ---
        Vector3 isoForward = new Vector3(1, 0, 1).normalized;
        Vector3 isoRight = new Vector3(1, 0, -1).normalized;

        moveInput = (isoForward * rawInput.y + isoRight * rawInput.x).normalized;

        // --- Movimiento ---
        transform.Translate(moveInput * moveSpeed * Time.deltaTime, Space.World);

        // --- Rotación suave ---
        if (moveInput != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        VirtualJoystick joystick = FindFirstObjectByType<VirtualJoystick>();
        if (joystick != null)
        {
            joystick.SetPlayer(this);
        }
    }
}