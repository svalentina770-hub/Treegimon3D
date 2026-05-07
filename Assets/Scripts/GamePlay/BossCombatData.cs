using System;
using UnityEngine;
using Unity.Netcode;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("VFX de combate")]
    [Tooltip("Prefab visual que se instancia cuando el boss usa ataque básico. Normalmente debe ser un proyectil.")]
    [SerializeField] private GameObject basicAttackVfxPrefab;

    [Tooltip("Prefab visual que se instancia cuando el boss usa ataque especial. Normalmente debe ser un proyectil o efecto grande.")]
    [SerializeField] private GameObject specialAttackVfxPrefab;

    [Tooltip("Prefab visual que se instancia cuando el boss usa defensa. Normalmente debe ser un escudo, aura o efecto local.")]
    [SerializeField] private GameObject defenseVfxPrefab;

    [Tooltip("Prefab opcional que aparece cuando un ataque del boss impacta al rival.")]
    [SerializeField] private GameObject impactVfxPrefab;

    [Header("Configuración VFX")]
    [SerializeField] private bool basicVfxTravelsToTarget = true;
    [SerializeField] private bool specialVfxTravelsToTarget = true;
    [SerializeField] private bool defenseVfxTravelsToTarget = false;

    [SerializeField] private float basicVfxMoveSpeed = 12f;
    [SerializeField] private float specialVfxMoveSpeed = 9f;
    [SerializeField] private float defenseVfxMoveSpeed = 0f;

    [SerializeField] private float basicVfxLifetime = 5f;
    [SerializeField] private float specialVfxLifetime = 5f;
    [SerializeField] private float defenseVfxLifetime = 2.5f;

    [SerializeField] private bool orientAttackVfxToTarget = true;
    [SerializeField] private bool orientDefenseVfxToTarget = false;

    [Header("Auto asignación VFX")]
    [Tooltip("Si está activo, en el Editor se asignan automáticamente prefabs aleatorios estables desde las carpetas de VFX configuradas.")]
    [SerializeField] private bool autoAssignVfxFromFolders = true;

    [SerializeField] private string basicAttacksFolder = "Assets/Prefabs/VFX/Combat/BasicAttacks";
    [SerializeField] private string defensesFolder = "Assets/Prefabs/VFX/Combat/Defenses";
    [SerializeField] private string specialAttacksFolder = "Assets/Prefabs/VFX/Combat/SpecialAttacks";
    [SerializeField] private string impactsFolder = "Assets/Prefabs/VFX/Combat/Impacts";

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

    public GameObject BasicAttackVfxPrefab => basicAttackVfxPrefab;
    public GameObject SpecialAttackVfxPrefab => specialAttackVfxPrefab;
    public GameObject DefenseVfxPrefab => defenseVfxPrefab;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;

    public bool BasicVfxTravelsToTarget => basicVfxTravelsToTarget;
    public bool SpecialVfxTravelsToTarget => specialVfxTravelsToTarget;
    public bool DefenseVfxTravelsToTarget => defenseVfxTravelsToTarget;

    public float BasicVfxMoveSpeed => basicVfxMoveSpeed;
    public float SpecialVfxMoveSpeed => specialVfxMoveSpeed;
    public float DefenseVfxMoveSpeed => defenseVfxMoveSpeed;

    public float BasicVfxLifetime => basicVfxLifetime;
    public float SpecialVfxLifetime => specialVfxLifetime;
    public float DefenseVfxLifetime => defenseVfxLifetime;

    public bool OrientAttackVfxToTarget => orientAttackVfxToTarget;
    public bool OrientDefenseVfxToTarget => orientDefenseVfxToTarget;

    public GameObject GetVfxPrefab(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return basicAttackVfxPrefab;

            case CombatActionType.SpecialAttack:
                return specialAttackVfxPrefab;

            case CombatActionType.Defense:
                return defenseVfxPrefab;

            default:
                return null;
        }
    }

    public bool VfxTravelsToTarget(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return basicVfxTravelsToTarget;

            case CombatActionType.SpecialAttack:
                return specialVfxTravelsToTarget;

            case CombatActionType.Defense:
                return defenseVfxTravelsToTarget;

            default:
                return false;
        }
    }

    public float GetVfxMoveSpeed(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return basicVfxMoveSpeed;

            case CombatActionType.SpecialAttack:
                return specialVfxMoveSpeed;

            case CombatActionType.Defense:
                return defenseVfxMoveSpeed;

            default:
                return 0f;
        }
    }

    public float GetVfxLifetime(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return basicVfxLifetime;

            case CombatActionType.SpecialAttack:
                return specialVfxLifetime;

            case CombatActionType.Defense:
                return defenseVfxLifetime;

            default:
                return 2.5f;
        }
    }

    public bool ShouldOrientVfxToTarget(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.BasicAttack:
            case CombatActionType.SpecialAttack:
                return orientAttackVfxToTarget;

            case CombatActionType.Defense:
                return orientDefenseVfxToTarget;

            default:
                return false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoAssignVfxFromFolders)
            return;

        AutoAssignVfxFromFolders();
    }

    [ContextMenu("Auto asignar VFX desde carpetas")]
    private void AutoAssignVfxFromFolders()
    {
        GameObject basic = LoadStablePrefabFromFolder(basicAttacksFolder, $"{bossId}_BasicAttack");
        if (basic != null)
            basicAttackVfxPrefab = basic;

        GameObject defense = LoadStablePrefabFromFolder(defensesFolder, $"{bossId}_Defense");
        if (defense != null)
            defenseVfxPrefab = defense;

        GameObject special = LoadStablePrefabFromFolder(specialAttacksFolder, $"{bossId}_SpecialAttack");
        if (special != null)
            specialAttackVfxPrefab = special;

        GameObject impact = LoadStablePrefabFromFolder(impactsFolder, $"{bossId}_Impact");
        if (impact != null)
            impactVfxPrefab = impact;

        EditorUtility.SetDirty(this);
    }

    private static GameObject LoadStablePrefabFromFolder(string folderPath, string stableSeed)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        if (!AssetDatabase.IsValidFolder(folderPath))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

        if (guids == null || guids.Length == 0)
            return null;

        Array.Sort(guids, StringComparer.Ordinal);

        int hash = string.IsNullOrWhiteSpace(stableSeed) ? 0 : stableSeed.GetHashCode();
        int index = Mathf.Abs(hash) % guids.Length;

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[index]);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
#endif
}