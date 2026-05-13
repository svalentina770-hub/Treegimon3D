using UnityEngine;
using Unity.Netcode;

public partial class DuelCombatManager
{
    private void PlayCombatVfxForAction(
        CombatSession session,
        CombatantState actor,
        CombatantState target,
        CombatActionType actionType)
    {
        if (session == null || actor == null)
            return;

        GameObject vfxPrefab = GetVfxPrefab(actor, actionType);
        bool usingImpactAsDefenseFallback = false;

        if (vfxPrefab == null && actionType == CombatActionType.Defense)
        {
            vfxPrefab = actor.impactVfxPrefab;
            usingImpactAsDefenseFallback = vfxPrefab != null;
        }

        if (vfxPrefab == null)
        {
            Debug.LogWarning($"DuelCombatManager: No hay VFX asignado para {GetDisplayName(actor)} / {actionType}.");
            return;
        }

        GameObject impactPrefab = actor.impactVfxPrefab;
        bool travelsToTarget = GetVfxTravelsToTarget(actor, actionType);
        float moveSpeed = GetVfxMoveSpeed(actor, actionType);
        float lifetime = GetVfxLifetime(actor, actionType);
        bool orientToTarget = GetVfxOrientToTarget(actor, actionType);

        bool actorIsA = actor == session.a;
        bool targetIsA = target == session.a;

        string resourceFolder = usingImpactAsDefenseFallback
            ? "VFX/Combat/Impacts"
            : GetResourceFolderForAction(actionType);

        string impactResourceFolder = "VFX/Combat/Impacts";

        string vfxPrefabName = vfxPrefab.name;
        string impactPrefabName = impactPrefab != null ? impactPrefab.name : string.Empty;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
        {
            PlayCombatVfxClientRpc(
                actorIsA,
                targetIsA,
                (int)actionType,
                resourceFolder,
                vfxPrefabName,
                impactResourceFolder,
                impactPrefabName,
                travelsToTarget,
                moveSpeed,
                lifetime,
                orientToTarget
            );
        }
        else
        {
            PlayCombatVfxLocal(
                actorIsA,
                targetIsA,
                (int)actionType,
                resourceFolder,
                vfxPrefabName,
                impactResourceFolder,
                impactPrefabName,
                travelsToTarget,
                moveSpeed,
                lifetime,
                orientToTarget
            );
        }
    }

    private GameObject GetVfxPrefab(CombatantState combatant, CombatActionType actionType)
    {
        if (combatant == null)
            return null;

        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return combatant.basicAttackVfxPrefab;

            case CombatActionType.SpecialAttack:
                return combatant.specialAttackVfxPrefab;

            case CombatActionType.Defense:
                return combatant.defenseVfxPrefab;

            default:
                return null;
        }
    }

    private bool GetVfxTravelsToTarget(CombatantState combatant, CombatActionType actionType)
    {
        if (combatant == null)
            return false;

        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return combatant.basicVfxTravelsToTarget;

            case CombatActionType.SpecialAttack:
                return combatant.specialVfxTravelsToTarget;

            case CombatActionType.Defense:
                return combatant.defenseVfxTravelsToTarget;

            default:
                return false;
        }
    }

    private float GetVfxMoveSpeed(CombatantState combatant, CombatActionType actionType)
    {
        if (combatant == null)
            return 0f;

        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return combatant.basicVfxMoveSpeed;

            case CombatActionType.SpecialAttack:
                return combatant.specialVfxMoveSpeed;

            case CombatActionType.Defense:
                return combatant.defenseVfxMoveSpeed;

            default:
                return 0f;
        }
    }

    private float GetVfxLifetime(CombatantState combatant, CombatActionType actionType)
    {
        if (combatant == null)
            return 2.5f;

        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return combatant.basicVfxLifetime;

            case CombatActionType.SpecialAttack:
                return combatant.specialVfxLifetime;

            case CombatActionType.Defense:
                return combatant.defenseVfxLifetime;

            default:
                return 2.5f;
        }
    }

    private bool GetVfxOrientToTarget(CombatantState combatant, CombatActionType actionType)
    {
        if (combatant == null)
            return false;

        switch (actionType)
        {
            case CombatActionType.BasicAttack:
            case CombatActionType.SpecialAttack:
                return combatant.orientAttackVfxToTarget;

            case CombatActionType.Defense:
                return combatant.orientDefenseVfxToTarget;

            default:
                return false;
        }
    }

    private string GetResourceFolderForAction(CombatActionType actionType)
    {
        switch (actionType)
        {
            case CombatActionType.BasicAttack:
                return "VFX/Combat/BasicAttacks";

            case CombatActionType.SpecialAttack:
                return "VFX/Combat/SpecialAttacks";

            case CombatActionType.Defense:
                return "VFX/Combat/Defenses";

            default:
                return string.Empty;
        }
    }

    [ClientRpc]
    private void PlayCombatVfxClientRpc(
        bool actorIsA,
        bool targetIsA,
        int actionTypeValue,
        string resourceFolder,
        string prefabName,
        string impactResourceFolder,
        string impactPrefabName,
        bool travelsToTarget,
        float moveSpeed,
        float lifetime,
        bool orientToTarget)
    {
        PlayCombatVfxLocal(
            actorIsA,
            targetIsA,
            actionTypeValue,
            resourceFolder,
            prefabName,
            impactResourceFolder,
            impactPrefabName,
            travelsToTarget,
            moveSpeed,
            lifetime,
            orientToTarget
        );
    }

    private void PlayCombatVfxLocal(
        bool actorIsA,
        bool targetIsA,
        int actionTypeValue,
        string resourceFolder,
        string prefabName,
        string impactResourceFolder,
        string impactPrefabName,
        bool travelsToTarget,
        float moveSpeed,
        float lifetime,
        bool orientToTarget)
    {
        if (string.IsNullOrWhiteSpace(resourceFolder) || string.IsNullOrWhiteSpace(prefabName))
            return;

        GameObject vfxPrefab = Resources.Load<GameObject>($"{resourceFolder}/{prefabName}");
        if (vfxPrefab == null)
        {
            Debug.LogWarning($"DuelCombatManager: No se encontró el VFX en Resources: {resourceFolder}/{prefabName}");
            return;
        }

        CombatActionType actionType = (CombatActionType)actionTypeValue;

        GameObject impactPrefab = null;
        if (!string.IsNullOrWhiteSpace(impactResourceFolder) && !string.IsNullOrWhiteSpace(impactPrefabName))
        {
            impactPrefab = Resources.Load<GameObject>($"{impactResourceFolder}/{impactPrefabName}");

            if (impactPrefab == null)
                Debug.LogWarning($"DuelCombatManager: No se encontró el Impact VFX en Resources: {impactResourceFolder}/{impactPrefabName}");
        }

        if (impactPrefab == null && actionType != CombatActionType.Defense)
            impactPrefab = LoadStableRandomResourcePrefab("VFX/Combat/Impacts", prefabName + "_ImpactFallback");

        CombatVFXPoints vfxPoints = FindLocalCombatVFXPoints();
        if (vfxPoints == null)
        {
            Debug.LogWarning("DuelCombatManager: No se encontró CombatVFXPoints en la arena local.");
            return;
        }

        Transform spawnPoint;
        Transform targetPoint;

        if (actionType == CombatActionType.Defense)
        {
            spawnPoint = vfxPoints.GetDefensePoint(actorIsA);
            targetPoint = spawnPoint;
        }
        else
        {
            spawnPoint = vfxPoints.GetAttackSpawn(actorIsA);
            targetPoint = vfxPoints.GetHitPoint(targetIsA);
        }

        if (spawnPoint == null)
            return;

        Quaternion spawnRotation = spawnPoint.rotation;

        if (orientToTarget && targetPoint != null)
        {
            Vector3 direction = targetPoint.position - spawnPoint.position;
            if (direction.sqrMagnitude > 0.001f)
                spawnRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        GameObject instance = Instantiate(vfxPrefab, spawnPoint.position, spawnRotation);

        if (actionType == CombatActionType.Defense)
        {
            instance.transform.SetParent(spawnPoint, true);
            instance.transform.position = spawnPoint.position;
            instance.transform.rotation = spawnRotation;
        }

        if (travelsToTarget && targetPoint != null)
        {
            CombatVFXProjectile projectile = instance.GetComponent<CombatVFXProjectile>();
            if (projectile == null)
                projectile = instance.AddComponent<CombatVFXProjectile>();

            projectile.Initialize(targetPoint, impactPrefab, moveSpeed, lifetime);
            return;
        }

        AutoDestroyVFX autoDestroy = instance.GetComponent<AutoDestroyVFX>();
        if (autoDestroy != null)
            autoDestroy.SetLifetime(lifetime);
        else
            Destroy(instance, Mathf.Max(0.1f, lifetime));
    }

    private GameObject LoadStableRandomResourcePrefab(string resourcesFolder, string stableSeed)
    {
        if (string.IsNullOrWhiteSpace(resourcesFolder))
            return null;

        GameObject[] prefabs = Resources.LoadAll<GameObject>(resourcesFolder);

        if (prefabs == null || prefabs.Length == 0)
            return null;

        int hash = string.IsNullOrWhiteSpace(stableSeed) ? 0 : stableSeed.GetHashCode();
        int index = Mathf.Abs(hash) % prefabs.Length;
        return prefabs[index];
    }

    private CombatVFXPoints FindLocalCombatVFXPoints()
    {
        CombatVFXPoints[] allPoints = FindObjectsOfType<CombatVFXPoints>(true);

        if (allPoints == null || allPoints.Length == 0)
            return null;

        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i] != null && allPoints[i].isActiveAndEnabled)
                return allPoints[i];
        }

        return allPoints[0];
    }
}
