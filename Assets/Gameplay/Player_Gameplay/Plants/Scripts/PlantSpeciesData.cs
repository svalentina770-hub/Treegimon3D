using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Species Data", fileName = "Plant_")]
public class PlantSpeciesData : ScriptableObject
{
    [Header("Identificación")]
    [Tooltip("ID único de la especie. Debe coincidir con id_base_especie del JSON/.tree. Ejemplo: aliso, roble, sietecueros.")]
    public string plantId;

    [Tooltip("Nombre visible de la especie. Ejemplo: Aliso, Roble, Sietecueros.")]
    public string displayName;

    [Tooltip("Nombre científico de la especie.")]
    public string scientificName;

    [TextArea(2, 5)]
    [Tooltip("Descripción educativa general de la especie.")]
    public string generalDescription;

    [TextArea(2, 5)]
    [Tooltip("Descripción de cuidados, necesidades o contexto ambiental de la especie.")]
    public string careDescription;

    [Header("Clasificación")]
    public PlantRarity rarity;
    public PlantBiomeType biomeType;

    [Tooltip("Rol sugerido en combate. Ejemplo: Atacante, Defensivo, Soporte, Balanceado.")]
    public string combatRole;

    [Header("Obtención")]
    [TextArea]
    public string obtainMethod;

    [Header("Stats base")]
    [Tooltip("HP base usado para combate antes de aplicar nivel, fase u otros modificadores.")]
    public int baseHP = 1200;

    [Tooltip("Daño base del ataque básico.")]
    public int baseAttack = 80;

    [Tooltip("Defensa base o valor de mitigación usado por el sistema de combate.")]
    public int baseDefense = 10;

    [Tooltip("Daño o potencia base de la habilidad especial.")]
    public int baseSpecialAttack = 120;

    [Range(0f, 1f)]
    [Tooltip("Porcentaje de reducción de daño al usar defensa. 0.25 equivale a 25%.")]
    public float defensePercent = 0.25f;

    [Header("Requisitos")]
    public int minLevelToPvP = 3;

    [Header("XP")]
    public int xpWin = 150;
    public int xpLose = 100;
    public int xpWinBiomeBonus = 250;
    public int xpLoseBiomeBonus = 150;

    [Header("Habilidades")]
    public AbilityData basicAttack;
    public AbilityData defenseSkill;
    public AbilityData specialSkill;

    [Header("Nombres de habilidades para UI")]
    [Tooltip("Texto de respaldo si basicAttack no tiene nombre configurado.")]
    public string basicAttackDisplayName = "Ataque básico";

    [Tooltip("Texto de respaldo si defenseSkill no tiene nombre configurado.")]
    public string defenseDisplayName = "Defensa";

    [Tooltip("Texto de respaldo si specialSkill no tiene nombre configurado.")]
    public string specialDisplayName = "Ataque especial";

    [Header("Visual")]
    [Tooltip("Prefab visual genérico de la especie. Las variantes específicas por estudiante se administran desde PlantDataBase.")]
    public GameObject worldVisualPrefab;

    public string GetBasicAttackName()
    {
        return string.IsNullOrWhiteSpace(basicAttackDisplayName) ? "Ataque básico" : basicAttackDisplayName;
    }

    public string GetDefenseName()
    {
        return string.IsNullOrWhiteSpace(defenseDisplayName) ? "Defensa" : defenseDisplayName;
    }

    public string GetSpecialName()
    {
        return string.IsNullOrWhiteSpace(specialDisplayName) ? "Ataque especial" : specialDisplayName;
    }
}