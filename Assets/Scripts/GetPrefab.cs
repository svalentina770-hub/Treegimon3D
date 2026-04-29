using UnityEngine;

public class GetPrefab : MonoBehaviour
{
    public GameObject Prefab;
    [SerializeField] private Vector3 plantScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private Vector3 plantSpawnPosition = new Vector3(0f, 0f, 0f);

    void Start()
    {
        GameObject manager = GameObject.Find("Manager");
        SelectedPrefab selector = manager.GetComponent<SelectedPrefab>();
        Prefab = selector.prefab1;

        GameObject plantInstance = Instantiate(Prefab, this.transform);
        plantInstance.transform.localPosition = plantSpawnPosition;
        plantInstance.transform.localRotation = Quaternion.identity;

        // Resetear escala al tamaño correcto para el juego
        plantInstance.transform.localScale = plantScale;
    }
}