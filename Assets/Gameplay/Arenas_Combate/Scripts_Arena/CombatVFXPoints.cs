using UnityEngine;

public class CombatVFXPoints : MonoBehaviour
{
    [Header("Ataques")]
    public Transform attackSpawnA;
    public Transform attackSpawnB;

    [Header("Impactos")]
    public Transform hitPointA;
    public Transform hitPointB;

    [Header("Defensas")]
    public Transform defensePointA;
    public Transform defensePointB;

    public Transform GetAttackSpawn(bool fromA)
    {
        return fromA ? attackSpawnA : attackSpawnB;
    }

    public Transform GetHitPoint(bool targetIsA)
    {
        return targetIsA ? hitPointA : hitPointB;
    }

    public Transform GetDefensePoint(bool defenderIsA)
    {
        return defenderIsA ? defensePointA : defensePointB;
    }
}