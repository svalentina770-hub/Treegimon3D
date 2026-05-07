using UnityEngine;
using Unity.Netcode;

public class BossCombatData : NetworkBehaviour
{
    [Header("Identidad")]
    [SerializeField] private string bossId = "boss_hidro";
    [SerializeField] private string bossDisplayName = "Guardián del Humedal";
    [SerializeField] private PlantBiomeType bossBiome = PlantBiomeType.Hidro;

    [Header("Stats de combate")]
    [SerializeField] private int maxHP = 1500;
    [SerializeField] private int basicAttackDamage = 100;
    [SerializeField] private int specialAttackDamage = 180;
    [SerializeField] private int defenseValue = 20;
    [SerializeField, Range(0f, 1f)] private float defensePercent = 0.25f;

    [Header("Habilidades")]
    [SerializeField] private string basicAttackName = "Ataque básico";
    [SerializeField] private string defenseName = "Defensa";
    [SerializeField] private string specialAttackName = "Ataque especial";

    public string BossId => bossId;
    public string BossDisplayName => bossDisplayName;
    public PlantBiomeType BossBiome => bossBiome;

    public int MaxHP => maxHP;
    public int BasicAttackDamage => basicAttackDamage;
    public int SpecialAttackDamage => specialAttackDamage;
    public int DefenseValue => defenseValue;
    public float DefensePercent => defensePercent;

    public string BasicAttackName => basicAttackName;
    public string DefenseName => defenseName;
    public string SpecialAttackName => specialAttackName;
}