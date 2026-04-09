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
    [Range(0f, 1f)] public float damageattackBuffPercent;

    public int buffDurationTurns = 0;
    public bool reducesIncomingDamage;
    [Range(0f, 1f)] public float damageReductionPercent;

    public bool stealsTurn;
    public bool disablesShield;
    public int disablesShieldDurationTurns;
}
