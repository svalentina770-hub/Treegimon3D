using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Ability Data", fileName = "Ability_")]
public class AbilityData : ScriptableObject
{
    [Header("Identificación")]
    public string abilityId;
    public string displayName;
    public AbilityKind abilityKind;

    [TextArea]
    public string description;

    [Header("Valores")]
    public int power;
    public float cooldownSeconds = 10f;
    public int maxUsesPerBattle = -1; // -1 = sin límite

    [Header("Efectos opcionales")]
    public bool grantsShield;
    public int shieldValue;

    public bool heals;
    public int healValue;
    public int healDurationTurns = 0;

    public bool buffsAttack;
    public int attackBuffPercent;
    public int buffDurationTurns = 0;

    public bool reducesIncomingDamage;
    [Range(0f, 1f)] public float damageReductionPercent;

    public bool stealsTurn;
    public bool disablesShield;
    public int disablesShieldDurationTurns;

    [Header("VFX de combate")]
    [Tooltip("Prefab visual que se instancia al usar esta habilidad. Para ataques puede ser un proyectil; para defensas puede ser un aura o escudo.")]
    public GameObject vfxPrefab;

    [Tooltip("Prefab opcional que se instancia cuando un proyectil impacta al rival o llega al punto de impacto.")]
    public GameObject impactVfxPrefab;

    [Tooltip("Si está activo, el VFX se moverá desde el punto de ataque hasta el punto de impacto del rival.")]
    public bool vfxTravelsToTarget = true;

    [Tooltip("Velocidad del VFX si viaja hacia el objetivo.")]
    public float vfxMoveSpeed = 12f;

    [Tooltip("Tiempo de vida para VFX que no viajan, como defensas, auras o efectos de curación.")]
    public float vfxLifetime = 2.5f;

    [Tooltip("Si está activo, el VFX se orienta hacia el objetivo al instanciarse.")]
    public bool orientVfxToTarget = true;
}
