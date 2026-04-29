using UnityEngine;

public class PlayerVisualLoader : MonoBehaviour
{
    [Header("Dónde se pondrá el modelo visual")]
    [SerializeField] private Transform visualRoot;

    [Header("Prefabs/modelos de plantas en el mismo orden del carrusel")]
    [SerializeField] private GameObject[] plantPrefabs;

    private void Start()
    {
        int selectedId = PlayerPrefs.GetInt("SelectedCharacterId", 1);
        int index = selectedId - 1;

        if (visualRoot == null)
        {
            Debug.LogWarning("PlayerVisualLoader: visualRoot no asignado.");
            return;
        }

        if (plantPrefabs == null || plantPrefabs.Length == 0)
        {
            Debug.LogWarning("PlayerVisualLoader: no hay prefabs de plantas asignados.");
            return;
        }

        // Borrar visual anterior
        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(visualRoot.GetChild(i).gameObject);
        }

        // Instanciar la planta elegida
        if (index >= 0 && index < plantPrefabs.Length)
        {
            GameObject plantInstance = Instantiate(plantPrefabs[index], visualRoot);
            plantInstance.transform.localPosition = Vector3.zero;
            plantInstance.transform.localRotation = Quaternion.identity;
            plantInstance.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.LogWarning("PlayerVisualLoader: índice fuera de rango.");
        }
    }
}