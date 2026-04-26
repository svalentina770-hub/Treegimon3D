using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Carrusel")]
    [SerializeField] private Transform carouselRoot;
    [SerializeField] private float spacing = 3f;
    [SerializeField] private float snapSpeed = 8f;

    [Header("Escala de personajes")]
    [SerializeField] private float centerScale = 1.6f;
    [SerializeField] private float sideScale = 0.9f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button playButton;

    [Header("Swipe")]
    [SerializeField] private float swipeThreshold = 50f;

    private List<CharacterData> _characters = new List<CharacterData>
    {
        new CharacterData { characterName = "Aliso",  characterId = 1 },
        new CharacterData { characterName = "Espino",     characterId = 2 },
        new CharacterData { characterName = "Helecho",    characterId = 3 },
        new CharacterData { characterName = "Roble",   characterId = 4 },
        new CharacterData { characterName = "Pino",    characterId = 5 },
        new CharacterData { characterName = "Pasto",      characterId = 6 },
    };

    private List<GameObject> _cubes = new List<GameObject>();
    private int _currentIndex = 0;

    public List<GameObject> planta = new List<GameObject>();

    public Material CubeTransparente;
    // Swipe
    private Vector2 _swipeStart;
    private bool _isSwiping = false;

    private void Start()
    {
        // Centrar el carouselRoot en la cámara
        carouselRoot.position = new Vector3(0, 0, 0);

        SpawnCharacters();

        leftButton.onClick.AddListener(() => Navigate(-1));
        rightButton.onClick.AddListener(() => Navigate(1));
        playButton.onClick.AddListener(OnPlay);

        UpdateUI();
    }

    private void SpawnCharacters()
    {
        for (int i = 0; i < _characters.Count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(carouselRoot);
            cube.transform.localPosition = new Vector3(i * spacing, 0, 0);
            cube.transform.localScale = Vector3.one * sideScale;
            

            Instantiate(planta[i], cube.transform.localPosition, Quaternion.identity, cube.transform);

            Renderer r = cube.GetComponent<Renderer>();
            r.enabled = false;
            //r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            r.material = CubeTransparente;
           // r.material.color = GetCharacterColor(i);

            _cubes.Add(cube);
        }
    }

    private Color GetCharacterColor(int index)
    {
        Color[] colors = {
            new Color(0.9f, 0.2f, 0.2f),
            new Color(0.3f, 0.7f, 0.3f),
            new Color(0.2f, 0.5f, 0.2f),
            new Color(0.8f, 0.4f, 0.8f),
            new Color(0.9f, 0.8f, 0.1f),
            new Color(0.4f, 0.6f, 0.2f),
        };
        return colors[index % colors.Length];
    }

    private bool _isLeaving = false;

    private void Update()
    {
        if (_isLeaving) return; // ← detiene todo si ya cambiamos de escena
        HandleSwipe();
        UpdateCarouselPositions();
        UpdateCharacterScales();
    }



    private void HandleSwipe()
    {
        // Touch — New Input System
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                _swipeStart = touch.position.ReadValue();
                _isSwiping = true;
            }

            if (touch.press.wasReleasedThisFrame && _isSwiping)
            {
                float delta = touch.position.ReadValue().x - _swipeStart.x;
                if (Mathf.Abs(delta) > swipeThreshold)
                    Navigate(delta > 0 ? -1 : 1);
                _isSwiping = false;
            }
        }

        // Mouse — para testear en editor
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _swipeStart = Mouse.current.position.ReadValue();
                _isSwiping = true;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame && _isSwiping)
            {
                float delta = Mouse.current.position.ReadValue().x - _swipeStart.x;
                if (Mathf.Abs(delta) > swipeThreshold)
                    Navigate(delta > 0 ? -1 : 1);
                _isSwiping = false;
            }
        }
    }
  

    private void Navigate(int direction)
    {
        int newIndex = _currentIndex + direction;
        if (newIndex < 0 || newIndex >= _characters.Count) return;
        _currentIndex = newIndex;
        UpdateUI();
    }

    private void UpdateCarouselPositions()
    {
        // Mover el root para centrar el cubo actual
        float targetX = -_currentIndex * spacing;
        Vector3 targetPos = new Vector3(targetX, 0, 0);
        carouselRoot.localPosition = Vector3.Lerp(
            carouselRoot.localPosition,
            targetPos,
            Time.deltaTime * snapSpeed
        );
    }

    private void UpdateCharacterScales()
    {
        for (int i = 0; i < _cubes.Count; i++)
        {
            float targetScale = (i == _currentIndex) ? centerScale : sideScale;
            _cubes[i].transform.localScale = Vector3.Lerp(
                _cubes[i].transform.localScale,
                Vector3.one * targetScale,
                Time.deltaTime * snapSpeed
            );

            if (i == _currentIndex)
                _cubes[i].transform.Rotate(0, 45f * Time.deltaTime, 0);
        }
    }

    private void UpdateUI()
    {
        characterNameText.text = _characters[_currentIndex].characterName;
        leftButton.interactable = _currentIndex > 0;
        rightButton.interactable = _currentIndex < _characters.Count - 1;
    }

    private void OnPlay()
    {
        _isLeaving = true; // ← activa la bandera antes de cargar
        CharacterData selected = _characters[_currentIndex];
        PlayerPrefs.SetInt("SelectedCharacterId", selected.characterId);
        PlayerPrefs.SetString("SelectedCharacterName", selected.characterName);
        PlayerPrefs.SetInt("SelectedColorIndex", _currentIndex);
        SceneManager.LoadScene(1);
    }

    public GameObject getPrefab()
    {
        return planta[_currentIndex];
    }
}