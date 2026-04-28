using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class PlayerPlantLoadout : NetworkBehaviour
{
    [SerializeField] private PlantDataBase plantDatabase;
    [SerializeField] private string debugDefaultPlantId = "aliso";
    [SerializeField] private int debugDefaultLevel = 3;

    public NetworkVariable<FixedString64Bytes> equippedPlantId =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> plantLevel =
        new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentXP =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlantSpeciesData resolvedPlantData;

    public override void OnNetworkSpawn()
    {
        equippedPlantId.OnValueChanged += OnPlantChanged;

        ResolveCurrentPlant();

        if (IsOwner)
        {
            string selectedPlantId = PlayerPrefs.GetString("SELECTED_PLANT_ID", debugDefaultPlantId);
            int selectedLevel = PlayerPrefs.GetInt("SELECTED_PLANT_LEVEL", debugDefaultLevel);

            SubmitLoadoutServerRpc(selectedPlantId, selectedLevel);
        }
    }

    public override void OnDestroy()
    {
        equippedPlantId.OnValueChanged -= OnPlantChanged;
        base.OnDestroy();
    }

    [ServerRpc]
    private void SubmitLoadoutServerRpc(string selectedPlantId, int selectedLevel)
    {
        if (string.IsNullOrWhiteSpace(selectedPlantId))
            selectedPlantId = debugDefaultPlantId;

        equippedPlantId.Value = selectedPlantId.Trim().ToLower();
        plantLevel.Value = Mathf.Max(1, selectedLevel);
    }

    private void OnPlantChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        ResolveCurrentPlant();
    }

    private void ResolveCurrentPlant()
    {
        if (plantDatabase == null)
        {
            Debug.LogWarning("No hay PlantDatabase asignado.");
            return;
        }

        string id = equippedPlantId.Value.ToString();
        if (string.IsNullOrWhiteSpace(id))
            id = debugDefaultPlantId;

        resolvedPlantData = plantDatabase.GetById(id);

        if (resolvedPlantData == null)
            Debug.LogWarning($"No se encontró la planta con id '{id}'.");
    }

    public PlantSpeciesData GetPlantData()
    {
        if (resolvedPlantData == null)
            ResolveCurrentPlant();

        return resolvedPlantData;
    }

    public bool CanChallenge()
    {
        PlantSpeciesData plant = GetPlantData();
        return plant != null && plantLevel.Value >= plant.minLevelToPvP;
    }

    public void AddXP(int amount)
    {
        if (!IsServer) return;
        currentXP.Value = Mathf.Max(0, currentXP.Value + amount);
    }

    public string GetPlantId()
    {
        return equippedPlantId.Value.ToString();
    }
}