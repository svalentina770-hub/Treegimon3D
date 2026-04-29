using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("State")]
    [HideInInspector] public Vector2 joystickInput;
    public bool canMove = true;

    private Vector3 moveInput;

    void Update()
    {
        if (!canMove) return;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector2 keyboardInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            float h = (Keyboard.current.dKey.isPressed ? 1 : 0)
                    - (Keyboard.current.aKey.isPressed ? 1 : 0);

            float v = (Keyboard.current.wKey.isPressed ? 1 : 0)
                    - (Keyboard.current.sKey.isPressed ? 1 : 0);

            keyboardInput = new Vector2(h, v);
        }

        Vector2 rawInput = keyboardInput.magnitude > 0 ? keyboardInput : joystickInput;

        if (rawInput.sqrMagnitude < 0.01f)
        {
            moveInput = Vector3.zero;
            return;
        }

        Vector3 forward;
        Vector3 right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();
        }
        else
        {
            forward = new Vector3(1, 0, 1).normalized;
            right = new Vector3(1, 0, -1).normalized;
        }

        moveInput = forward * rawInput.y + right * rawInput.x;

        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        transform.position += moveInput * moveSpeed * Time.deltaTime;

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
        // Temporalmente se omite la validación IsOwner para permitir pruebas locales sin Netcode.

        VirtualJoystick joystick = FindFirstObjectByType<VirtualJoystick>();
        if (joystick != null)
        {
            joystick.SetPlayer(this);
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    public void SetCanMove(bool value)
    {
        canMove = value;

        if (!canMove)
            joystickInput = Vector2.zero;
    }
}