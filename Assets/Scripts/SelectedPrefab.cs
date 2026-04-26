using UnityEngine;

public class SelectedPrefab : MonoBehaviour
{
    public CharacterSelectManager selector;

    public GameObject prefab1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DontDestroyOnLoad(this.transform);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void getPrefab() {
       prefab1 = selector.getPrefab();
    }
}
