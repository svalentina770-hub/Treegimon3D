using UnityEngine;

public partial class DuelCombatManager
{
    private void AutoResolveTurn(CombatSession session)
    {
        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);

        if (attacker.isBoss)
        {
            ResolveBossAutoAction(session, attacker, defender);
            return;
        }

        if (CanUseBasic(attacker))
        {
            ResolveAttack(session, attacker, defender, attacker.plant != null ? attacker.plant.basicAttack : null, false);
            return;
        }

        if (CanUseSpecial(attacker))
        {
            ResolveAttack(session, attacker, defender, attacker.plant != null ? attacker.plant.specialSkill : null, true);
            return;
        }

        AdvanceTurn(session, "Tiempo agotado");
    }

    private void ResolveBossAutoAction(CombatSession session, CombatantState boss, CombatantState defender)
    {
        boss.turnsTaken++;

        // Condición temporal para probar defensa del boss en su primer turno.
        // Luego puedes volver a:
        // bool shouldUseDefense = boss.currentHP < boss.maxHP * 0.35f && CanUseDefense(boss);
        bool shouldUseDefense = boss.turnsTaken == 1 && CanUseDefense(boss);

        if (shouldUseDefense)
        {
            ArmDefense(boss);
            PlayCombatVfxForAction(session, boss, boss, CombatActionType.Defense);
            AdvanceTurn(session, $"{GetDisplayName(boss)} usó {boss.defenseName}");
            return;
        }

        bool shouldUseSpecial = bossSpecialEveryTurns > 0 && boss.turnsTaken % bossSpecialEveryTurns == 0 && CanUseSpecial(boss);
        ResolveAttack(session, boss, defender, null, shouldUseSpecial);
    }

    private void ResolveAttack(CombatSession session, CombatantState attacker, CombatantState defender, AbilityData ability, bool isSpecial)
    {
        int damage = CalculateBaseDamage(session, attacker, ability, isSpecial);

        if (damage <= 0)
        {
            AdvanceTurn(session, "No hay habilidad configurada");
            return;
        }

        bool hasBiomeBonus =
            session.combatBiome != PlantBiomeType.Templado &&
            attacker.biomeType == session.combatBiome;

        if (hasBiomeBonus)
            damage = Mathf.RoundToInt(damage * (1f + biomeDamageBonusPercent / 100f));

        if (attacker.attackBuffTurnsRemaining > 0 && attacker.attackBuffPercent > 0)
            damage += Mathf.RoundToInt(damage * (attacker.attackBuffPercent / 100f));

        if (defender.defenseArmed)
        {
            damage = Mathf.Max(0, damage - defender.armedShieldValue);

            if (defender.armedDamageReduction > 0f)
                damage = Mathf.RoundToInt(damage * (1f - defender.armedDamageReduction));

            defender.defenseArmed = false;
            defender.armedShieldValue = 0;
            defender.armedDamageReduction = 0f;
        }

        defender.currentHP = Mathf.Max(0, defender.currentHP - damage);

        if (ability != null && ability.heals)
        {
            int healAmount = attacker.baseDefense + ability.healValue;
            attacker.currentHP = Mathf.Min(attacker.maxHP, attacker.currentHP + healAmount);
        }

        if (ability != null && ability.buffsAttack)
        {
            attacker.attackBuffPercent = ability.attackBuffPercent;
            attacker.attackBuffTurnsRemaining = Mathf.Max(1, ability.buffDurationTurns);
        }

        if (ability != null && ability.disablesShield)
            defender.shieldDisabledTurnsRemaining = Mathf.Max(1, ability.disablesShieldDurationTurns);

        if (isSpecial)
            attacker.specialCooldownRemaining = ability != null ? Mathf.Max(0f, ability.cooldownSeconds) : turnDurationSeconds;
        else
            attacker.basicCooldownRemaining = ability != null ? Mathf.Max(0f, ability.cooldownSeconds) : 0f;

        if (attacker.attackBuffTurnsRemaining > 0)
            attacker.attackBuffTurnsRemaining--;

        string actionName = GetActionName(attacker, ability, isSpecial);

        PlayCombatVfxForAction(
            session,
            attacker,
            defender,
            isSpecial ? CombatActionType.SpecialAttack : CombatActionType.BasicAttack
        );

        if (defender.currentHP <= 0)
        {
            StartCoroutine(FinishCombatRoutine(session, attacker, defender));
            return;
        }

        if (ability != null && ability.stealsTurn)
        {
            session.turnTimerRemaining = turnDurationSeconds;
            session.lastBroadcastedSecond = -1;
            BroadcastSessionState(session, $"{GetDisplayName(attacker)} usó {actionName} y conserva el turno");
            return;
        }

        AdvanceTurn(session, $"{GetDisplayName(attacker)} usó {actionName}");
    }

    private int CalculateBaseDamage(CombatSession session, CombatantState attacker, AbilityData ability, bool isSpecial)
    {
        if (attacker.isBoss)
            return isSpecial ? attacker.baseSpecialAttack : attacker.baseAttack;

        if (attacker.plant == null)
            return 0;

        if (ability == null)
        {
            return isSpecial
                ? Mathf.Max(1, attacker.baseSpecialAttack)
                : Mathf.Max(1, attacker.baseAttack);
        }

        return attacker.baseAttack + ability.power;
    }

    private void ArmDefense(CombatantState defender)
    {
        defender.defenseUsesRemaining = Mathf.Max(0, defender.defenseUsesRemaining - 1);
        defender.defenseArmed = true;

        if (defender.isBoss)
        {
            defender.armedShieldValue = defender.baseDefense;
            defender.armedDamageReduction = defender.defensePercent;
            return;
        }

        AbilityData defense = defender.plant != null ? defender.plant.defenseSkill : null;

        if (defense == null)
        {
            defender.armedShieldValue = defender.baseDefense;
            defender.armedDamageReduction = defender.plant != null ? defender.plant.defensePercent : 0.25f;
            return;
        }

        defender.armedShieldValue = defense.grantsShield
            ? defender.baseDefense + defense.shieldValue
            : 0;

        defender.armedDamageReduction = defense.reducesIncomingDamage
            ? defense.damageReductionPercent
            : 0f;
    }

    private void AdvanceTurn(CombatSession session, string statusMessage)
    {
        session.attackerIsA = !session.attackerIsA;
        session.turnTimerRemaining = turnDurationSeconds;
        session.lastBroadcastedSecond = -1;

        ReduceTurnLockouts(session.a);
        ReduceTurnLockouts(session.b);

        BroadcastSessionState(session, statusMessage);
    }

    private void ReduceTurnLockouts(CombatantState combatant)
    {
        if (combatant.shieldDisabledTurnsRemaining > 0)
            combatant.shieldDisabledTurnsRemaining--;
    }

    private bool CanUseBasic(CombatantState combatant)
    {
        if (combatant == null)
            return false;

        if (combatant.isBoss)
            return combatant.basicCooldownRemaining <= 0f;

        return combatant.plant != null && combatant.basicCooldownRemaining <= 0f;
    }

    private bool CanUseSpecial(CombatantState combatant)
    {
        if (combatant == null)
            return false;

        if (combatant.isBoss)
            return combatant.specialCooldownRemaining <= 0f;

        return combatant.plant != null && combatant.specialCooldownRemaining <= 0f;
    }

    private bool CanUseDefense(CombatantState combatant)
    {
        if (combatant == null)
            return false;

        if (combatant.isBoss)
        {
            return !combatant.defenseArmed
                && combatant.defenseUsesRemaining > 0
                && combatant.shieldDisabledTurnsRemaining <= 0;
        }

        return combatant.plant != null
            && !combatant.defenseArmed
            && combatant.defenseUsesRemaining > 0
            && combatant.shieldDisabledTurnsRemaining <= 0;
    }

    private CombatantState GetAttacker(CombatSession session)
    {
        return session.attackerIsA ? session.a : session.b;
    }

    private CombatantState GetDefender(CombatSession session)
    {
        return session.attackerIsA ? session.b : session.a;
    }

    private string GetTurnStatusMessage(CombatSession session)
    {
        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);

        if (attacker.isBoss)
            return $"{GetDisplayName(attacker)} prepara su acción";

        return $"{GetDisplayName(attacker)} ataca - {GetDisplayName(defender)} puede defender";
    }

    private string GetDisplayName(CombatantState combatant)
    {
        if (combatant == null)
            return "Combatiente";

        if (!string.IsNullOrWhiteSpace(combatant.displayName))
            return combatant.displayName;

        if (combatant.plant != null && !string.IsNullOrWhiteSpace(combatant.plant.displayName))
            return combatant.plant.displayName;

        return combatant.isBoss ? "Boss" : "Planta";
    }

    private string GetActionName(CombatantState attacker, AbilityData ability, bool isSpecial)
    {
        if (ability != null && !string.IsNullOrWhiteSpace(ability.displayName))
            return ability.displayName;

        return isSpecial ? attacker.specialAttackName : attacker.basicAttackName;
    }
}
