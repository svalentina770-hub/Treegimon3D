using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Penguin Wobble")]
    public float bobHeight = 0.15f;      // Altura del saltito
    public float bobSpeed = 10f;         // Velocidad del saltito
    public float wobbleAngle = 12f;      // Grados del bamboleo lateral

    [HideInInspector] public Vector2 joystickInput;

    private Vector3 moveInput;
    private float _bobTime;
    private float _baseY;

    private void Start()
    {
        _baseY = transform.position.y;
    }

    void Update()
    {
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

        // --- Combinar inputs ---
        Vector2 rawInput = keyboardInput.magnitude > 0 ? keyboardInput : joystickInput;

        // --- Dirección isométrica ---
        Vector3 isoForward = new Vector3(1, 0, 1).normalized;
        Vector3 isoRight = new Vector3(1, 0, -1).normalized;
        moveInput = (isoForward * rawInput.y + isoRight * rawInput.x).normalized;

        // --- Movimiento ---
        transform.Translate(moveInput * moveSpeed * Time.deltaTime, Space.World);

        // --- Rotación suave hacia dirección ---
        if (moveInput != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // --- Efecto pingüino ---
        if (rawInput.magnitude > 0)
        {
            _bobTime += Time.deltaTime * bobSpeed;

            // Saltito vertical
            float newY = _baseY + Mathf.Abs(Mathf.Sin(_bobTime)) * bobHeight;

            // Bamboleo lateral (en el eje local Z del player)
            float wobble = Mathf.Sin(_bobTime) * wobbleAngle;

            transform.position = new Vector3(
                transform.position.x,
                newY,
                transform.position.z
            );

            // Aplicar bamboleo sobre la rotación actual
            transform.rotation *= Quaternion.Euler(0, 0, wobble);
        }
        else
        {
            // Al detenerse, volver suavemente a la posición base
            _bobTime = 0;
            transform.position = new Vector3(
                transform.position.x,
                Mathf.Lerp(transform.position.y, _baseY, Time.deltaTime * 5f),
                transform.position.z
            );
        }
    }
}