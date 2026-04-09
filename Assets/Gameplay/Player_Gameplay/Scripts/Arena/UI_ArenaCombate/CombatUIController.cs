using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUIController : MonoBehaviour
{
    public static CombatUIController LocalInstance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject combatPanel;

    [Header("Top Bar")]
    [SerializeField] private TextMeshProUGUI myNameText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI rivalNameText;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("My HP")]
    [SerializeField] private Slider myHPSlider;
    [SerializeField] private TextMeshProUGUI myHPValueText;

    [Header("Rival HP")]
    [SerializeField] private Slider rivalHPSlider;
    [SerializeField] private TextMeshProUGUI rivalHPValueText;

    [Header("Buttons")]
    [SerializeField] private Button attackButton;
    [SerializeField] private Button defenseButton;
    [SerializeField] private Button specialButton;

    private Canvas rootCanvas;
    private CanvasScaler rootScaler;
    private GraphicRaycaster rootRaycaster;

    private void Awake()
    {
        if (LocalInstance != null && LocalInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        LocalInstance = this;

        rootCanvas = GetComponent<Canvas>();
        rootScaler = GetComponent<CanvasScaler>();
        rootRaycaster = GetComponent<GraphicRaycaster>();

        ConfigureRootCanvas();

        if (attackButton != null)
            attackButton.onClick.AddListener(OnAttackPressed);

        if (defenseButton != null)
            defenseButton.onClick.AddListener(OnDefensePressed);

        if (specialButton != null)
            specialButton.onClick.AddListener(OnSpecialPressed);

        HideCombatUIImmediate();
    }

    private void OnDestroy()
    {
        if (LocalInstance == this)
            LocalInstance = null;
    }

    private void ConfigureRootCanvas()
    {
        if (rootCanvas != null)
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (rootScaler != null)
        {
            rootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            rootScaler.referenceResolution = new Vector2(1920, 1080);
            rootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            rootScaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform rt = transform as RectTransform;
        if (rt != null)
            rt.localScale = Vector3.one;
    }

    public void ShowCombatUI(
        string myName,
        string rivalName,
        int myCurrentHP,
        int myMaxHP,
        int rivalCurrentHP,
        int rivalMaxHP)
    {
        gameObject.SetActive(true);

        if (combatPanel != null)
            combatPanel.SetActive(true);

        if (rootCanvas != null)
            rootCanvas.enabled = true;

        if (rootRaycaster != null)
            rootRaycaster.enabled = true;

        if (myNameText != null)
            myNameText.text = myName;

        if (rivalNameText != null)
            rivalNameText.text = rivalName;

        SetMyHP(myCurrentHP, myMaxHP);
        SetRivalHP(rivalCurrentHP, rivalMaxHP);
        SetTimerDisplay(5);
        SetStatus("Esperando acción...");
        SetButtonsInteractable(true);
    }

    public void HideCombatUI()
    {
        if (combatPanel != null)
            combatPanel.SetActive(false);

        if (rootCanvas != null)
            rootCanvas.enabled = false;

        if (rootRaycaster != null)
            rootRaycaster.enabled = false;

        gameObject.SetActive(false);
    }

    private void HideCombatUIImmediate()
    {
        if (combatPanel != null)
            combatPanel.SetActive(false);

        if (rootCanvas != null)
            rootCanvas.enabled = false;

        if (rootRaycaster != null)
            rootRaycaster.enabled = false;

        gameObject.SetActive(false);
    }

    public void SetMyHP(int current, int max)
    {
        if (myHPSlider != null)
        {
            myHPSlider.maxValue = max;
            myHPSlider.value = current;
        }

        if (myHPValueText != null)
            myHPValueText.text = $"{current}/{max}";
    }

    public void SetRivalHP(int current, int max)
    {
        if (rivalHPSlider != null)
        {
            rivalHPSlider.maxValue = max;
            rivalHPSlider.value = current;
        }

        if (rivalHPValueText != null)
            rivalHPValueText.text = $"{current}/{max}";
    }

    public void SetTimerDisplay(int secondsRemaining)
    {
        if (timerText != null)
            timerText.text = $"00:{secondsRemaining:00}";
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    public void SetButtonsInteractable(bool value)
    {
        if (attackButton != null) attackButton.interactable = value;
        if (defenseButton != null) defenseButton.interactable = value;
        if (specialButton != null) specialButton.interactable = value;
    }

    private void OnAttackPressed()
    {
        Debug.Log("[CombatUI] AttackButton presionado");
        SetStatus("Ataque genérico seleccionado");
    }

    private void OnDefensePressed()
    {
        Debug.Log("[CombatUI] DefenseButton presionado");
        SetStatus("Defensa seleccionada");
    }

    private void OnSpecialPressed()
    {
        Debug.Log("[CombatUI] SpecialButton presionado");
        SetStatus("Especial seleccionada");
    }
}