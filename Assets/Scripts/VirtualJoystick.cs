using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    private PlayerMovement player;

    [Header("Settings")]
    [SerializeField] private float handleRange = 1f;

    private Vector2 _inputVector;
    private float _radius;

    public Vector2 InputVector => _inputVector;

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

        _radius = background.sizeDelta.x * 0.5f;
    }

    public void SetPlayer(PlayerMovement newPlayer)
    {
        player = newPlayer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint
        );

        Vector2 direction = localPoint / _radius;
        _inputVector = direction.magnitude > 1f ? direction.normalized : direction;

        handle.anchoredPosition = _inputVector * _radius * handleRange;

        if (player != null)
            player.joystickInput = _inputVector;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;

        if (player != null)
            player.joystickInput = Vector2.zero;
    }
}