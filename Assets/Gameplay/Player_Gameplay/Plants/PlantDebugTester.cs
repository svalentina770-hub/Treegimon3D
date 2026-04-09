using UnityEngine;

public class PlantDebugTester : MonoBehaviour
{
    [SerializeField] private PlayerPlantLoadout loadout;

    private void Start()
    {
        if (loadout == null)
        {
            Debug.LogWarning("No hay loadout asignado.");
            return;
        }

        Debug.Log("Planta equipada: " + loadout.GetPlantName());
        Debug.Log("Puede retar: " + loadout.CanChallenge());
        Debug.Log("Bioma: " + loadout.GetBiomeType());
    }
}