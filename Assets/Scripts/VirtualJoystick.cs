using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private PlayerMovement player;

    [Header("Settings")]
    [SerializeField] private float handleRange = 1f; // multiplicador del radio

    private Vector2 _inputVector;
    private float _radius;

    private void Start()
    {
        _radius = background.sizeDelta.x * 0.5f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Convertir posición touch a coordenadas locales del joystick
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint
        );

        // Normalizar dentro del círculo
        Vector2 direction = localPoint / _radius;
        _inputVector = direction.magnitude > 1f ? direction.normalized : direction;

        // Mover el handle visualmente
        handle.anchoredPosition = _inputVector * _radius * handleRange;

        // Enviar al player
        player.joystickInput = _inputVector;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        player.joystickInput = Vector2.zero;
    }
}
