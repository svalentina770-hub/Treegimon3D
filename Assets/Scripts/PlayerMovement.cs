using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float joystickRotationDeadZone = 0.15f;
    [SerializeField] private bool rotatePlayerWithInput = true;
    [SerializeField] private bool rotateCameraWithInput = true;
    [SerializeField] private bool rotateCameraAroundPlayer = true;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private SmoothCameraFollow smoothCameraFollow;

    [Header("Virtual Joysticks")]
    [SerializeField] private VirtualJoystick movementJoystick;
    [SerializeField] private VirtualJoystick rotationJoystick;

    [Header("Control")]
    [SerializeField] private bool canMove = true;

    private Rigidbody rb;

    private Vector2 movementJoystickInput;
    private Vector2 rotationJoystickInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        AutoAssignCameraIfNeeded();
        BindJoysticks();
    }

    private void Update()
    {
        if (!CanControlThisPlayer())
            return;

        if (!canMove)
            return;

        HandleRotation();
    }

    private void FixedUpdate()
    {
        if (!CanControlThisPlayer())
            return;

        if (!canMove)
            return;

        HandleMovement();
    }

    private bool CanControlThisPlayer()
    {
        if (NetworkManager.Singleton == null)
            return true;

        if (!NetworkManager.Singleton.IsListening)
            return true;

        return IsOwner;
    }

    private void HandleMovement()
    {
        Vector2 keyboardInput = GetKeyboardMovementInput();
        Vector2 finalInput = keyboardInput;

        if (movementJoystickInput.sqrMagnitude > finalInput.sqrMagnitude)
            finalInput = movementJoystickInput;

        Vector3 moveDirection = GetCameraRelativeMoveDirection(finalInput);

        if (rb != null)
        {
            Vector3 targetVelocity = moveDirection * moveSpeed;
            Vector3 currentVelocity = rb.linearVelocity;

            rb.linearVelocity = new Vector3(
                targetVelocity.x,
                currentVelocity.y,
                targetVelocity.z
            );
        }
        else
        {
            transform.position += moveDirection * moveSpeed * Time.fixedDeltaTime;
        }
    }

    private void HandleRotation()
    {
        float keyboardRotation = GetKeyboardRotationInput();
        float joystickRotation = Mathf.Abs(rotationJoystickInput.x) > joystickRotationDeadZone
            ? rotationJoystickInput.x
            : 0f;

        float finalRotationInput = Mathf.Abs(joystickRotation) > Mathf.Abs(keyboardRotation)
            ? joystickRotation
            : keyboardRotation;

        if (Mathf.Abs(finalRotationInput) <= 0.001f)
            return;

        float rotationAmount = finalRotationInput * rotationSpeed * Time.deltaTime;

        if (rotatePlayerWithInput)
            transform.Rotate(0f, rotationAmount, 0f, Space.World);

        if (rotateCameraWithInput)
            RotateCameraWithInput(rotationAmount);
    }

    private void RotateCameraWithInput(float rotationAmount)
    {
        AutoAssignCameraIfNeeded();

        if (cameraTransform == null)
            return;

        if (smoothCameraFollow != null)
        {
            smoothCameraFollow.RotateAroundCurrentTarget(rotationAmount, true);
            return;
        }

        if (rotateCameraAroundPlayer)
            cameraTransform.RotateAround(transform.position, Vector3.up, rotationAmount);
        else
            cameraTransform.Rotate(0f, rotationAmount, 0f, Space.World);
    }

    private Vector2 GetKeyboardMovementInput()
    {
        Vector2 input = Vector2.zero;

        if (Input.GetKey(KeyCode.W))
            input.y += 1f;

        if (Input.GetKey(KeyCode.S))
            input.y -= 1f;

        if (Input.GetKey(KeyCode.D))
            input.x += 1f;

        if (Input.GetKey(KeyCode.A))
            input.x -= 1f;

        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private float GetKeyboardRotationInput()
    {
        float input = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            input -= 1f;

        if (Input.GetKey(KeyCode.RightArrow))
            input += 1f;

        return input;
    }

    private Vector3 GetCameraRelativeMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        Transform referenceTransform = cameraTransform != null ? cameraTransform : transform;

        Vector3 forward = referenceTransform.forward;
        Vector3 right = referenceTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * input.y + right * input.x;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        return moveDirection;
    }

    private void AutoAssignCameraIfNeeded()
    {
        Camera mainCamera = Camera.main;

        if (cameraTransform == null && mainCamera != null)
            cameraTransform = mainCamera.transform;

        if (smoothCameraFollow == null && mainCamera != null)
            smoothCameraFollow = mainCamera.GetComponent<SmoothCameraFollow>();
    }

    private void BindJoysticks()
    {
        if (movementJoystick != null)
            movementJoystick.SetPlayer(this, VirtualJoystick.JoystickPurpose.Movement);

        if (rotationJoystick != null)
            rotationJoystick.SetPlayer(this, VirtualJoystick.JoystickPurpose.Rotation);
    }

    public void SetMovementJoystickInput(Vector2 input)
    {
        movementJoystickInput = input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public void SetRotationJoystickInput(Vector2 input)
    {
        rotationJoystickInput = input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    public void SetCanMove(bool value)
    {
        canMove = value;

        if (!canMove && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}