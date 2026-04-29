using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;

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
    [SerializeField] private TextMeshProUGUI speciesNameText;
    [SerializeField] private TextMeshProUGUI studentNameText;
    [SerializeField] private TextMeshProUGUI estadoText;
    [SerializeField] private TextMeshProUGUI progresoText;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button playButton;

    [Header("Carga automática de modelos")]
    [Tooltip("Ruta usada solo en el Editor para localizar los .obj dentro de Assets.")]
    [SerializeField] private string editorModelsBaseFolderPath = "Assets/Resources/Modelos_3D";

    [Tooltip("Ruta base dentro de Resources. Ejemplo físico: Assets/Resources/Modelos_3D/fase1, fase2, fase3 y fase4")]
    [SerializeField] private string resourcesModelsBaseFolderPath = "Modelos_3D";

    [Header("Datos de usuario")]
    [Tooltip("Ruta del JSON dentro de Resources, sin extensión. Ejemplo físico: Assets/Resources/Data/Data_user.json")]
    [SerializeField] private string userDataResourcePath = "Data/Data_user";

    [Header("Swipe")]
    [SerializeField] private float swipeThreshold = 50f;

    public Material CubeTransparente;

    private List<CarouselCharacterData> _characters = new List<CarouselCharacterData>();
    private List<GameObject> _cubes = new List<GameObject>();
    private List<GameObject> planta = new List<GameObject>();
    private List<ModelDisplayData> _modelDisplayData = new List<ModelDisplayData>();
    private List<CarouselItemData> _carouselItems = new List<CarouselItemData>();

    private Dictionary<string, List<GameObject>> _modelsByPhase = new Dictionary<string, List<GameObject>>();

    private UserPlantDatabase _userPlantDatabase;

    private int _currentIndex = 0;

    private Vector2 _swipeStart;
    private bool _isSwiping = false;
    private bool _isLeaving = false;

    private void Start()
    {
        if (carouselRoot != null)
            carouselRoot.position = new Vector3(0, 0, 0);

        LoadModelsFromFolder();
        LoadUserDataFromJson();
        BuildCarouselItemsFromJson();

        SpawnCharacters();

        if (leftButton != null)
            leftButton.onClick.AddListener(() => Navigate(-1));

        if (rightButton != null)
            rightButton.onClick.AddListener(() => Navigate(1));

        if (playButton != null)
            playButton.onClick.AddListener(OnPlay);

        UpdateUI();
    }

    private void Update()
    {
        if (_isLeaving)
            return;

        HandleSwipe();
        UpdateCarouselPositions();
        UpdateCharacterScales();
    }

    #region Carga de modelos

    private void LoadModelsFromFolder()
    {
        planta.Clear();
        _modelsByPhase.Clear();

#if UNITY_EDITOR
        LoadModelsFromAssetDatabase();
#else
        LoadModelsFromResources();
#endif

        planta.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        SortPhaseModelLists();

        BuildCharactersFromLoadedModels();

        Debug.Log($"Modelos cargados para el carrusel: {planta.Count}");
    }

#if UNITY_EDITOR
    private void LoadModelsFromAssetDatabase()
    {
        if (!AssetDatabase.IsValidFolder(editorModelsBaseFolderPath))
        {
            Debug.LogWarning($"No se encontró la carpeta base de modelos del Editor: {editorModelsBaseFolderPath}. Se intentará cargar desde Resources.");
            LoadModelsFromResources();
            return;
        }

        string[] phaseFolders = { "fase1", "fase2", "fase3", "fase4" };

        for (int i = 0; i < phaseFolders.Length; i++)
        {
            string phaseFolder = phaseFolders[i];
            string editorPhasePath = $"{editorModelsBaseFolderPath}/{phaseFolder}";

            if (!AssetDatabase.IsValidFolder(editorPhasePath))
            {
                Debug.LogWarning($"No se encontró la carpeta del Editor: {editorPhasePath}");
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { editorPhasePath });

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!assetPath.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (model != null)
                {
                    planta.Add(model);
                    RegisterModelByPhase(model, phaseFolder);
                }
            }

            Debug.Log($"Modelos .obj cargados desde AssetDatabase/{phaseFolder}: {guids.Length}");
        }

        if (planta.Count == 0)
        {
            Debug.LogWarning($"No se encontraron modelos .obj en {editorModelsBaseFolderPath}/fase1..fase4. Se intentará cargar desde Resources.");
            LoadModelsFromResources();
        }
    }
#endif

    private void LoadModelsFromResources()
    {
        string[] phaseFolders = { "fase1", "fase2", "fase3", "fase4" };
        int totalLoaded = 0;

        for (int i = 0; i < phaseFolders.Length; i++)
        {
            string phaseFolder = phaseFolders[i];
            string resourcePath = $"{resourcesModelsBaseFolderPath}/{phaseFolder}";

            GameObject[] loadedModels = Resources.LoadAll<GameObject>(resourcePath);

            if (loadedModels == null || loadedModels.Length == 0)
            {
                Debug.LogWarning($"No se encontraron modelos en Resources/{resourcePath}. Ruta física esperada: Assets/Resources/{resourcePath}");
                continue;
            }

            foreach (GameObject model in loadedModels)
            {
                if (model == null)
                    continue;

                planta.Add(model);
                RegisterModelByPhase(model, phaseFolder);
                totalLoaded++;
            }

            Debug.Log($"Modelos cargados desde Resources/{resourcePath}: {loadedModels.Length}");
        }

        if (totalLoaded == 0)
        {
            Debug.LogError($"No se encontró ningún modelo en Resources/{resourcesModelsBaseFolderPath}/fase1..fase4. Para Android/WebGL los modelos deben estar físicamente en Assets/Resources/{resourcesModelsBaseFolderPath}/fase#");
        }
    }

    private void RegisterModelByPhase(GameObject model, string phaseFolder)
    {
        if (model == null)
            return;

        string normalizedPhase = NormalizePhaseFolderName(phaseFolder);

        if (string.IsNullOrWhiteSpace(normalizedPhase))
            normalizedPhase = "fase4";

        if (!_modelsByPhase.ContainsKey(normalizedPhase))
            _modelsByPhase[normalizedPhase] = new List<GameObject>();

        if (!_modelsByPhase[normalizedPhase].Contains(model))
            _modelsByPhase[normalizedPhase].Add(model);
    }

    private string NormalizePhaseFolderName(string phaseFolder)
    {
        if (string.IsNullOrWhiteSpace(phaseFolder))
            return string.Empty;

        phaseFolder = phaseFolder.ToLowerInvariant().Trim();

        if (phaseFolder.Contains("fase1")) return "fase1";
        if (phaseFolder.Contains("fase2")) return "fase2";
        if (phaseFolder.Contains("fase3")) return "fase3";
        if (phaseFolder.Contains("fase4")) return "fase4";

        return phaseFolder;
    }

    private void SortPhaseModelLists()
    {
        foreach (KeyValuePair<string, List<GameObject>> phaseModels in _modelsByPhase)
        {
            phaseModels.Value.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    private string GetPhaseFolderFromPlantState(PlantStateJsonData estado)
    {
        if (estado == null || string.IsNullOrWhiteSpace(estado.fase))
            return "fase4";

        string phase = estado.fase.ToLowerInvariant().Trim();

        switch (phase)
        {
            case "semilla":
                return "fase1";

            case "arbusto":
                return "fase2";

            case "arbol":
            case "árbol":
                return "fase3";

            case "ent":
                return "fase4";

            default:
                Debug.LogWarning($"Fase no reconocida en JSON: '{estado.fase}'. Se usará fase4 por defecto.");
                return "fase4";
        }
    }

    #endregion

    #region Carga de JSON

    private void LoadUserDataFromJson()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>(userDataResourcePath);

        if (jsonAsset == null)
        {
            Debug.LogError($"No se encontró el JSON en Resources/{userDataResourcePath}. El archivo debe estar en Assets/Resources/Data/Data_user.json");
            _userPlantDatabase = null;
            return;
        }

        _userPlantDatabase = JsonUtility.FromJson<UserPlantDatabase>(jsonAsset.text);

        if (_userPlantDatabase == null || _userPlantDatabase.plantas == null)
        {
            Debug.LogError("El JSON de usuario no se pudo leer correctamente o no contiene el arreglo plantas.");
            _userPlantDatabase = null;
            return;
        }

        Debug.Log($"Datos de usuario cargados desde Resources/{userDataResourcePath}. Plantas en JSON: {_userPlantDatabase.plantas.Count}");
    }

    private void BuildCarouselItemsFromJson()
    {
        _characters.Clear();
        _modelDisplayData.Clear();
        _carouselItems.Clear();

        if (_userPlantDatabase == null || _userPlantDatabase.plantas == null || _userPlantDatabase.plantas.Count == 0)
        {
            Debug.LogWarning("No hay datos de plantas en el JSON. Se usará la lista de modelos cargados como respaldo.");
            BuildCharactersFromLoadedModels();

            for (int i = 0; i < planta.Count; i++)
            {
                _carouselItems.Add(new CarouselItemData
                {
                    prefab = planta[i],
                    displayData = _modelDisplayData[i],
                    estado = null,
                    progreso = null,
                    unlocked = true
                });
            }

            return;
        }

        for (int i = 0; i < _userPlantDatabase.plantas.Count; i++)
        {
            PlantJsonData plantData = _userPlantDatabase.plantas[i];

            if (plantData == null)
                continue;

            AddCarouselItemFromJsonPlant(plantData);
        }

        Debug.Log($"Items construidos para el carrusel desde JSON: {_carouselItems.Count}");
    }

    private void AddCarouselItemFromJsonPlant(PlantJsonData plantData)
    {
        string speciesName = !string.IsNullOrWhiteSpace(plantData.nombre_especie)
            ? plantData.nombre_especie
            : FormatTextForUI(plantData.id);

        string studentName = !string.IsNullOrWhiteSpace(plantData.nombre_estudiante)
            ? plantData.nombre_estudiante
            : "Sin modelo asignado";

        bool isUnlocked = plantData.desbloqueada;

        GameObject prefab = isUnlocked
            ? FindPrefabForJsonItem(plantData.id, speciesName, studentName, plantData.estado)
            : null;

        ModelDisplayData displayData = new ModelDisplayData
        {
            speciesName = speciesName,
            studentName = studentName,
            fullDisplayName = $"{speciesName} - {studentName}"
        };

        CarouselItemData item = new CarouselItemData
        {
            prefab = prefab,
            displayData = displayData,
            estado = plantData.estado,
            progreso = plantData.progreso,
            unlocked = isUnlocked,
            plantId = plantData.id,
            baseSpeciesId = plantData.id_base_especie,
            instanceId = plantData.id_instancia,
            subspeciesId = plantData.id_subespecie
        };

        _carouselItems.Add(item);
        _modelDisplayData.Add(displayData);

        _characters.Add(new CarouselCharacterData
        {
            characterName = displayData.fullDisplayName,
            characterId = _characters.Count + 1
        });
    }

    private GameObject FindPrefabForJsonItem(string plantId, string speciesName, string studentName, PlantStateJsonData estado)
    {
        string phaseFolder = GetPhaseFolderFromPlantState(estado);

        string normalizedPlantId = NormalizeForMatching(plantId);
        string normalizedSpeciesName = NormalizeForMatching(speciesName);
        string normalizedStudentName = NormalizeForMatching(studentName);

        if (!_modelsByPhase.TryGetValue(phaseFolder, out List<GameObject> phaseModels) || phaseModels == null || phaseModels.Count == 0)
        {
            Debug.LogWarning($"No hay modelos cargados para la fase '{phaseFolder}'. Especie '{speciesName}', estudiante '{studentName}'.");
            return null;
        }

        for (int i = 0; i < phaseModels.Count; i++)
        {
            ModelDisplayData modelData = ExtractModelDisplayData(phaseModels[i].name);

            string modelSpecies = NormalizeForMatching(modelData.speciesName);
            string modelStudent = NormalizeForMatching(modelData.studentName);
            string modelRawName = NormalizeForMatching(phaseModels[i].name);

            bool speciesMatches =
                modelSpecies == normalizedSpeciesName ||
                modelSpecies == normalizedPlantId ||
                modelRawName.Contains(normalizedPlantId);

            bool studentMatches =
                !string.IsNullOrWhiteSpace(normalizedStudentName) &&
                modelStudent == normalizedStudentName;

            if (speciesMatches && studentMatches)
                return phaseModels[i];
        }

        Debug.LogWarning($"No se encontró prefab en '{phaseFolder}' para especie '{speciesName}' y estudiante '{studentName}'. El item aparecerá sin modelo.");
        return null;
    }

    #endregion

    #region Construcción del carrusel

    private void BuildCharactersFromLoadedModels()
    {
        _characters.Clear();
        _modelDisplayData.Clear();

        for (int i = 0; i < planta.Count; i++)
        {
            ModelDisplayData displayData = ExtractModelDisplayData(planta[i].name);
            _modelDisplayData.Add(displayData);

        _characters.Add(new CarouselCharacterData
        {
            characterName = displayData.fullDisplayName,
            characterId = i + 1
        });
        }
    }

    private void SpawnCharacters()
    {
        if (_carouselItems.Count == 0)
        {
            Debug.LogWarning("No hay items cargados para mostrar en el carrusel.");
            return;
        }

        for (int i = 0; i < _carouselItems.Count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(carouselRoot);
            cube.transform.localPosition = new Vector3(i * spacing, 0, 0);
            cube.transform.localScale = Vector3.one * sideScale;

            if (_carouselItems[i].unlocked && _carouselItems[i].prefab != null)
            {
                Instantiate(_carouselItems[i].prefab, cube.transform);
            }

            Renderer r = cube.GetComponent<Renderer>();
            r.enabled = false;

            if (CubeTransparente != null)
                r.material = CubeTransparente;

            _cubes.Add(cube);
        }
    }

    private void Navigate(int direction)
    {
        int newIndex = _currentIndex + direction;

        if (newIndex < 0 || newIndex >= _characters.Count)
            return;

        _currentIndex = newIndex;
        UpdateUI();
    }

    private void UpdateCarouselPositions()
    {
        if (carouselRoot == null)
            return;

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

    #endregion

    #region UI e interacción

    private void HandleSwipe()
    {
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

    private void UpdateUI()
    {
        if (_characters.Count == 0)
        {
            SetText(speciesNameText, "Sin modelos");
            SetText(studentNameText, "");
            SetText(estadoText, "");
            SetText(progresoText, "");

            SetButtons(false, false, false);
            return;
        }

        if (_carouselItems.Count == 0 || _currentIndex < 0 || _currentIndex >= _carouselItems.Count)
        {
            SetText(speciesNameText, "Sin información");
            SetText(studentNameText, "");
            SetText(estadoText, "");
            SetText(progresoText, "");
        }
        else
        {
            CarouselItemData currentItem = _carouselItems[_currentIndex];

            SetText(speciesNameText, currentItem.displayData.speciesName);
            SetText(studentNameText, currentItem.displayData.studentName);
            SetText(estadoText, FormatEstadoText(currentItem.estado));
            SetText(progresoText, FormatProgresoText(currentItem.progreso));
        }

        SetButtons(
            _currentIndex > 0,
            _currentIndex < _characters.Count - 1,
            true
        );
    }

    private void SetButtons(bool left, bool right, bool play)
    {
        if (leftButton != null)
            leftButton.interactable = left;

        if (rightButton != null)
            rightButton.interactable = right;

        if (playButton != null)
            playButton.interactable = play;
    }

    private void SetText(TextMeshProUGUI textComponent, string value)
    {
        if (textComponent != null)
            textComponent.text = value;
    }

    private string FormatEstadoText(PlantStateJsonData estado)
    {
        if (estado == null)
            return "Estado: sin información";

        return $"Estado\nFase: {estado.fase}\nSalud: {estado.salud}\nHP: {estado.hp_actual}";
    }

    private string FormatProgresoText(PlantProgressJsonData progreso)
    {
        if (progreso == null)
            return "Progreso: sin información";

        return $"Progreso\nNivel: {progreso.nivel}\nXP: {progreso.xp}";
    }

    private void OnPlay()
    {
        _isLeaving = true;

        if (_characters.Count == 0)
        {
            Debug.LogWarning("No se puede iniciar porque no hay modelos cargados.");
            _isLeaving = false;
            return;
        }

        CarouselCharacterData selected = _characters[_currentIndex];

        PlayerPrefs.SetInt("SelectedCharacterId", selected.characterId);
        PlayerPrefs.SetString("SelectedCharacterName", selected.characterName);
        PlayerPrefs.SetInt("SelectedColorIndex", _currentIndex);

        if (_carouselItems.Count > 0 && _currentIndex >= 0 && _currentIndex < _carouselItems.Count)
        {
            CarouselItemData selectedItem = _carouselItems[_currentIndex];
            PlayerPrefs.SetString("SelectedPlantId", selectedItem.plantId);
            PlayerPrefs.SetString("SelectedBaseSpeciesId", selectedItem.baseSpeciesId);
            PlayerPrefs.SetString("SelectedInstanceId", selectedItem.instanceId);
            PlayerPrefs.SetInt("SelectedSubspeciesId", selectedItem.subspeciesId);
        }

        SceneManager.LoadScene(1);
    }

    public GameObject getPrefab()
    {
        if (_carouselItems.Count == 0 || _currentIndex < 0 || _currentIndex >= _carouselItems.Count)
            return null;

        return _carouselItems[_currentIndex].prefab;
    }

    #endregion

    #region Utilidades de nombres

    private ModelDisplayData ExtractModelDisplayData(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return new ModelDisplayData
            {
                speciesName = "Especie sin nombre",
                studentName = "Estudiante sin nombre",
                fullDisplayName = "Modelo sin nombre"
            };
        }

        string fileName = Path.GetFileNameWithoutExtension(rawName).Trim();
        string[] parts = fileName.Split('_');

        int faseIndex = FindFaseIndex(parts);
        int usefulLength = faseIndex >= 0 ? faseIndex : parts.Length;

        if (usefulLength <= 0)
        {
            return new ModelDisplayData
            {
                speciesName = "Especie sin nombre",
                studentName = "Estudiante sin nombre",
                fullDisplayName = FormatTextForUI(fileName)
            };
        }

        int speciesWordCount = GetSpeciesWordCount(parts, usefulLength);

        string speciesName = JoinParts(parts, 0, speciesWordCount);
        string studentName = JoinParts(parts, speciesWordCount, usefulLength - speciesWordCount);

        if (string.IsNullOrWhiteSpace(speciesName))
            speciesName = "Especie sin nombre";

        if (string.IsNullOrWhiteSpace(studentName))
            studentName = "Estudiante sin nombre";

        return new ModelDisplayData
        {
            speciesName = speciesName,
            studentName = studentName,
            fullDisplayName = $"{speciesName} - {studentName}"
        };
    }

    private int FindFaseIndex(string[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].ToLowerInvariant();

            if (part.StartsWith("fase"))
                return i;
        }

        return -1;
    }

    private int GetSpeciesWordCount(string[] parts, int usefulLength)
    {
        if (usefulLength <= 1)
            return usefulLength;

        string firstPart = parts[0].ToLowerInvariant();

        if (firstPart == "alcaparro" && usefulLength >= 2)
            return 2;

        if (firstPart == "pino" && usefulLength >= 2)
            return 2;

        if (firstPart == "cucharo" && usefulLength >= 2)
            return 2;

        return 1;
    }

    private string JoinParts(string[] parts, int startIndex, int count)
    {
        if (parts == null || count <= 0 || startIndex < 0 || startIndex >= parts.Length)
            return string.Empty;

        List<string> selectedParts = new List<string>();

        for (int i = startIndex; i < startIndex + count && i < parts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parts[i]))
                selectedParts.Add(parts[i]);
        }

        return FormatTextForUI(string.Join(" ", selectedParts));
    }

    private string FormatTextForUI(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("_", " ").Trim();

        while (text.Contains("  "))
            text = text.Replace("  ", " ");

        return text;
    }

    private string NormalizeForMatching(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.ToLowerInvariant();
        text = text.Replace("_", " ");
        text = text.Trim();

        while (text.Contains("  "))
            text = text.Replace("  ", " ");

        return text;
    }

    #endregion
}

[System.Serializable]
public class CarouselCharacterData
{
    public string characterName;
    public int characterId;
}

[System.Serializable]
public class ModelDisplayData
{
    public string speciesName;
    public string studentName;
    public string fullDisplayName;
}

[System.Serializable]
public class CarouselItemData
{
    public GameObject prefab;
    public ModelDisplayData displayData;
    public PlantStateJsonData estado;
    public PlantProgressJsonData progreso;
    public bool unlocked;
    public string plantId;
    public string baseSpeciesId;
    public string instanceId;
    public int subspeciesId;
}

[System.Serializable]
public class UserPlantDatabase
{
    public UserJsonData usuario;
    public ResourcesJsonData recursos;
    public List<PlantJsonData> plantas;
}

[System.Serializable]
public class UserJsonData
{
    public string id;
    public string nombre;
    public int nivel;
    public int xp;
}

[System.Serializable]
public class ResourcesJsonData
{
    public ResourceItemJsonData agua;
    public ResourceItemJsonData sol;
    public ResourceItemJsonData composta;
}

[System.Serializable]
public class ResourceItemJsonData
{
    public int cantidad;
    public int cooldown;
}

[System.Serializable]
public class PlantJsonData
{
    public string id;
    public string id_base_especie;
    public int id_subespecie;
    public string id_instancia;
    public string nombre_especie;
    public string nombre_cientifico;
    public string nombre_estudiante;
    public bool desbloqueada;
    public PlantStateJsonData estado;
    public PlantProgressJsonData progreso;
    public PlantVisualStateJsonData visual_estado;
    public PlantUsageJsonData uso;
    public AppliedResourcesJsonData recursos_aplicados;
}


[System.Serializable]
public class PlantStateJsonData
{
    public string fase;
    public string salud;
    public int hp_actual;
}

[System.Serializable]
public class PlantProgressJsonData
{
    public int nivel;
    public int xp;
}

[System.Serializable]
public class PlantVisualStateJsonData
{
    public string skin;
    public string variacion;
}

[System.Serializable]
public class PlantUsageJsonData
{
    public bool seleccionada;
    public bool en_combate;
}

[System.Serializable]
public class AppliedResourcesJsonData
{
    public int agua;
    public int sol;
    public int composta;
}