using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum JoystickPurpose
    {
        Movement,
        Rotation
    }

    [Header("References")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Player")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private JoystickPurpose joystickPurpose = JoystickPurpose.Movement;

    [Header("Auto asignación")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private float searchInterval = 0.25f;
    [SerializeField] private float maxSearchTime = 20f;

    [Header("Settings")]
    [SerializeField] private float handleRange = 1f;

    [Header("Movimiento")]
    [SerializeField] private bool invertMovementX = false;
    [SerializeField] private bool invertMovementY = false;
    [SerializeField] private float movementInputMultiplier = 1f;
    [SerializeField] private bool clampMovementInput = true;

    [Header("Rotación")]
    [SerializeField] private bool horizontalOnlyForRotation = true;
    [SerializeField] private bool invertRotationX = false;
    [SerializeField, Range(0f, 1f)] private float rotationDeadZone = 0.15f;
    [SerializeField] private float rotationInputMultiplier = 1f;
    [SerializeField] private bool clampRotationInput = true;

    private Vector2 inputVector;
    private float radius;
    private Coroutine findPlayerRoutine;

    public Vector2 InputVector => inputVector;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<RectTransform>();

        if (handle == null && transform.childCount > 0)
            handle = transform.GetChild(0).GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (background == null || handle == null)
        {
            Debug.LogError("VirtualJoystick: faltan referencias de background o handle en el Inspector.");
            enabled = false;
            return;
        }

        radius = background.sizeDelta.x * 0.5f;
        SendInputToPlayer(Vector2.zero);

        if (autoFindPlayer && player == null)
            BeginFindPlayerRoutine();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && autoFindPlayer && player == null)
            BeginFindPlayerRoutine();
    }

    private void OnDisable()
    {
        if (findPlayerRoutine != null)
        {
            StopCoroutine(findPlayerRoutine);
            findPlayerRoutine = null;
        }
    }

    private void BeginFindPlayerRoutine()
    {
        if (findPlayerRoutine != null)
            StopCoroutine(findPlayerRoutine);

        findPlayerRoutine = StartCoroutine(CoFindPlayerWhenSpawned());
    }

    private IEnumerator CoFindPlayerWhenSpawned()
    {
        float elapsed = 0f;

        while (player == null && elapsed < maxSearchTime)
        {
            TryAssignPlayerAutomatically();

            if (player != null)
                break;

            elapsed += searchInterval;
            yield return new WaitForSeconds(searchInterval);
        }

        findPlayerRoutine = null;
    }

    [ContextMenu("Buscar Player ahora")]
    public void TryAssignPlayerAutomatically()
    {
        PlayerMovement foundPlayer = FindLocalPlayerMovement();

        if (foundPlayer == null)
            return;

        SetPlayer(foundPlayer, joystickPurpose);
    }

    private PlayerMovement FindLocalPlayerMovement()
    {
        PlayerMovement[] players = FindObjectsOfType<PlayerMovement>(true);

        if (players == null || players.Length == 0)
            return null;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerMovement candidate = players[i];

            if (candidate == null)
                continue;

            NetworkObject networkObject = candidate.GetComponent<NetworkObject>();

            if (networkObject != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    if (networkObject.IsOwner)
                        return candidate;

                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(playerTag) && candidate.CompareTag(playerTag))
                return candidate;

            if (!string.IsNullOrWhiteSpace(playerObjectName) && candidate.gameObject.name.Contains(playerObjectName))
                return candidate;
        }

        return players[0];
    }

    public void SetPlayer(PlayerMovement newPlayer)
    {
        player = newPlayer;
        SendInputToPlayer(inputVector);
    }

    public void SetPlayer(PlayerMovement newPlayer, JoystickPurpose purpose)
    {
        player = newPlayer;
        joystickPurpose = purpose;
        SendInputToPlayer(inputVector);
    }

    public void SetPurpose(JoystickPurpose purpose)
    {
        joystickPurpose = purpose;
        SendInputToPlayer(inputVector);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        Vector2 direction = localPoint / radius;
        inputVector = direction.magnitude > 1f ? direction.normalized : direction;

        Vector2 visualInput = inputVector;
        Vector2 valueToSend = BuildValueToSend(inputVector);

        if (joystickPurpose == JoystickPurpose.Rotation && horizontalOnlyForRotation)
            visualInput = new Vector2(inputVector.x, 0f);

        handle.anchoredPosition = visualInput * radius * handleRange;

        SendInputToPlayer(valueToSend);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        SendInputToPlayer(Vector2.zero);
    }

    private Vector2 BuildValueToSend(Vector2 rawInput)
    {
        switch (joystickPurpose)
        {
            case JoystickPurpose.Movement:
                return BuildMovementInput(rawInput);

            case JoystickPurpose.Rotation:
                return BuildRotationInput(rawInput);

            default:
                return rawInput;
        }
    }

    private Vector2 BuildMovementInput(Vector2 rawInput)
    {
        Vector2 value = rawInput;

        if (invertMovementX)
            value.x *= -1f;

        if (invertMovementY)
            value.y *= -1f;

        value *= Mathf.Max(0f, movementInputMultiplier);

        if (clampMovementInput && value.sqrMagnitude > 1f)
            value.Normalize();

        return value;
    }

    private Vector2 BuildRotationInput(Vector2 rawInput)
    {
        float rotationX = rawInput.x;

        if (Mathf.Abs(rotationX) < rotationDeadZone)
            rotationX = 0f;

        if (invertRotationX)
            rotationX *= -1f;

        rotationX *= Mathf.Max(0f, rotationInputMultiplier);

        if (clampRotationInput)
            rotationX = Mathf.Clamp(rotationX, -1f, 1f);

        return horizontalOnlyForRotation
            ? new Vector2(rotationX, 0f)
            : new Vector2(rotationX, rawInput.y);
    }

    private void SendInputToPlayer(Vector2 value)
    {
        if (player == null)
        {
            if (autoFindPlayer)
                TryAssignPlayerAutomatically();

            if (player == null)
                return;
        }

        switch (joystickPurpose)
        {
            case JoystickPurpose.Movement:
                player.SetMovementJoystickInput(value);
                break;

            case JoystickPurpose.Rotation:
                player.SetRotationJoystickInput(value);
                break;
        }
    }
}