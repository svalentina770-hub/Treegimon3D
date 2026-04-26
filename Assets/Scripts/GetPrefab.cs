using UnityEngine;

public class GetPrefab : MonoBehaviour
{
    public GameObject Prefab;
    [SerializeField] private Vector3 plantScale = new Vector3(1f, 1f, 1f);

    void Start()
    {
        GameObject manager = GameObject.Find("Manager");
        SelectedPrefab selector = manager.GetComponent<SelectedPrefab>();
        Prefab = selector.prefab1;

        GameObject plantInstance = Instantiate(Prefab, Vector3.zero, Quaternion.identity, this.transform);

        // Resetear escala al tamaño correcto para el juego
        plantInstance.transform.localScale = plantScale;
    }
}