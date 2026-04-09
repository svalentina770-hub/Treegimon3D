using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Species Data", fileName = "Plant_")]
public class PlantSpeciesData : ScriptableObject
{
    [Header("Identificación")]
    public string plantId;
    public string displayName;

    [Header("Clasificación")]
    public PlantRarity rarity;
    public PlantBiomeType biomeType;

    [Header("Obtención")]
    [TextArea]
    public string obtainMethod;

    [Header("Stats base")]
    public int baseHP = 0;
    public int baseAttack = 0;
    public int baseDefense = 0;

    [Header("Requisitos")]
    public int minLevelToPvP = 3;

    [Header("XP")]
    public int xpWin = 0;
    public int xpLose = 0;
    public int xpWinBiomeBonus = 0;
    public int xpLoseBiomeBonus = 0;

    [Header("Habilidades")]
    public AbilityData basicAttack;
    public AbilityData defenseSkill;
    public AbilityData specialSkill;

    [Header("Visual")]
    public GameObject worldVisualPrefab;
}