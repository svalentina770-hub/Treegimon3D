using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class DuelCombatManager : MonoBehaviour
{
    public static DuelCombatManager Instance { get; private set; }

    [SerializeField] private float turnDurationSeconds = 5f;
    [SerializeField] private float postFinishDelaySeconds = 2f;
    [SerializeField] private float biomeDamageBonusPercent = 10f;

    private readonly Dictionary<int, CombatSession> sessionsByDuelId = new();
    private readonly Dictionary<ulong, int> duelIdByPlayer = new();

    private class CombatantState
    {
        public ulong clientId;
        public PlayerCombatBridge bridge;
        public PlayerPlantLoadout loadout;
        public PlantSpeciesData plant;

        public int maxHP;
        public int currentHP;

        public float basicCooldownRemaining;
        public float specialCooldownRemaining;

        public int defenseUsesRemaining = 2;
        public bool defenseArmed;
        public int armedShieldValue;
        public float armedDamageReduction;

        public int attackBuffPercent;
        public int attackBuffTurnsRemaining;

        public int shieldDisabledTurnsRemaining;
    }

    private class CombatSession
    {
        public int duelId;
        public CombatantState a;
        public CombatantState b;

        public ulong attackerId;
        public float turnTimerRemaining;
        public int lastBroadcastedSecond = -1;
        public bool finished;
        public PlantBiomeType combatBiome;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        foreach (CombatSession session in sessionsByDuelId.Values)
        {
            if (session.finished) continue;

            TickCooldowns(session, Time.deltaTime);

            session.turnTimerRemaining -= Time.deltaTime;
            int displaySeconds = Mathf.Max(0, Mathf.CeilToInt(session.turnTimerRemaining));

            if (displaySeconds != session.lastBroadcastedSecond)
            {
                session.lastBroadcastedSecond = displaySeconds;
                BroadcastSessionState(session, GetTurnStatusMessage(session));
            }

            if (session.turnTimerRemaining <= 0f)
            {
                AutoResolveTurn(session);
            }
        }
    }

    public void StartCombatSession(int duelId, ulong challengerId, ulong challengedId, PlantBiomeType combatBiome)
    {
        if (sessionsByDuelId.ContainsKey(duelId))
            return;

        CombatantState attacker = BuildCombatant(challengerId);
        CombatantState defender = BuildCombatant(challengedId);

        if (attacker == null || defender == null)
        {
            Debug.LogWarning("No se pudo iniciar sesión de combate. Falta loadout o bridge.");
            return;
        }

        CombatSession session = new CombatSession
        {
            duelId = duelId,
            a = attacker,
            b = defender,
            attackerId = challengerId,
            turnTimerRemaining = turnDurationSeconds,
            lastBroadcastedSecond = -1,
            finished = false,
            combatBiome = combatBiome
        };

        sessionsByDuelId[duelId] = session;
        duelIdByPlayer[challengerId] = duelId;
        duelIdByPlayer[challengedId] = duelId;

        ShowInitialUI(session);
        BroadcastSessionState(session, GetTurnStatusMessage(session));
    }

    public void ReceivePlayerAction(ulong clientId, CombatActionType actionType)
    {
        if (!duelIdByPlayer.TryGetValue(clientId, out int duelId))
            return;

        if (!sessionsByDuelId.TryGetValue(duelId, out CombatSession session))
            return;

        if (session.finished)
            return;

        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);

        if (clientId == attacker.clientId)
        {
            if (actionType == CombatActionType.BasicAttack)
            {
                if (!CanUseBasic(attacker))
                    return;

                ResolveAttack(session, attacker, defender, attacker.plant.basicAttack, false);
                return;
            }

            if (actionType == CombatActionType.SpecialAttack)
            {
                if (!CanUseSpecial(attacker))
                    return;

                ResolveAttack(session, attacker, defender, attacker.plant.specialSkill, true);
                return;
            }

            return;
        }

        if (clientId == defender.clientId)
        {
            if (actionType == CombatActionType.Defense && CanUseDefense(defender))
            {
                ArmDefense(defender);
                BroadcastSessionState(session, $"{defender.plant.displayName} activó defensa");
            }
        }
    }

    public void RemoveSessionSilently(int duelId)
    {
        if (!sessionsByDuelId.TryGetValue(duelId, out CombatSession session))
            return;

        duelIdByPlayer.Remove(session.a.clientId);
        duelIdByPlayer.Remove(session.b.clientId);
        sessionsByDuelId.Remove(duelId);
    }

    private CombatantState BuildCombatant(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData))
            return null;

        if (clientData.PlayerObject == null)
            return null;

        PlayerCombatBridge bridge = clientData.PlayerObject.GetComponent<PlayerCombatBridge>();
        if (bridge == null)
            return null;

        PlayerPlantLoadout loadout = bridge.GetLoadout();
        if (loadout == null)
            return null;

        PlantSpeciesData plant = loadout.GetPlantData();
        if (plant == null)
            return null;

        return new CombatantState
        {
            clientId = clientId,
            bridge = bridge,
            loadout = loadout,
            plant = plant,
            maxHP = plant.baseHP,
            currentHP = plant.baseHP,
            basicCooldownRemaining = 0f,
            specialCooldownRemaining = 0f,
            defenseUsesRemaining = 2,
            defenseArmed = false,
            armedShieldValue = 0,
            armedDamageReduction = 0f,
            attackBuffPercent = 0,
            attackBuffTurnsRemaining = 0,
            shieldDisabledTurnsRemaining = 0
        };
    }

    private void ShowInitialUI(CombatSession session)
    {
        ClientRpcParams aParams = BuildTargetParams(session.a.clientId);
        ClientRpcParams bParams = BuildTargetParams(session.b.clientId);

        string aBasicName = session.a.plant.basicAttack != null
            ? session.a.plant.basicAttack.displayName
            : "Ataque";

        string aDefenseName = session.a.plant.defenseSkill != null
            ? session.a.plant.defenseSkill.displayName
            : "Defensa";

        string aSpecialName = session.a.plant.specialSkill != null
            ? session.a.plant.specialSkill.displayName
            : "Especial";

        string bBasicName = session.b.plant.basicAttack != null
            ? session.b.plant.basicAttack.displayName
            : "Ataque";

        string bDefenseName = session.b.plant.defenseSkill != null
            ? session.b.plant.defenseSkill.displayName
            : "Defensa";

        string bSpecialName = session.b.plant.specialSkill != null
            ? session.b.plant.specialSkill.displayName
            : "Especial";

        session.a.bridge.ShowCombatUIClientRpc(
            session.a.plant.displayName,
            session.b.plant.displayName,
            session.a.currentHP,
            session.a.maxHP,
            session.b.currentHP,
            session.b.maxHP,
            aBasicName,
            aDefenseName,
            aSpecialName,
            aParams
        );

        session.b.bridge.ShowCombatUIClientRpc(
            session.b.plant.displayName,
            session.a.plant.displayName,
            session.b.currentHP,
            session.b.maxHP,
            session.a.currentHP,
            session.a.maxHP,
            bBasicName,
            bDefenseName,
            bSpecialName,
            bParams
        );
    }

    private void BroadcastSessionState(CombatSession session, string statusMessage)
    {
        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);

        int secondsRemaining = Mathf.Max(0, Mathf.CeilToInt(session.turnTimerRemaining));

        session.a.bridge.UpdateCombatUIClientRpc(
            session.a.currentHP,
            session.a.maxHP,
            session.b.currentHP,
            session.b.maxHP,
            secondsRemaining,
            session.a.clientId == attacker.clientId && CanUseBasic(session.a),
            session.a.clientId == defender.clientId && CanUseDefense(session.a),
            session.a.clientId == attacker.clientId && CanUseSpecial(session.a),
            Mathf.CeilToInt(session.a.basicCooldownRemaining),
            Mathf.CeilToInt(session.a.specialCooldownRemaining),
            session.a.defenseUsesRemaining,
            statusMessage,
            BuildTargetParams(session.a.clientId)
        );

        session.b.bridge.UpdateCombatUIClientRpc(
            session.b.currentHP,
            session.b.maxHP,
            session.a.currentHP,
            session.a.maxHP,
            secondsRemaining,
            session.b.clientId == attacker.clientId && CanUseBasic(session.b),
            session.b.clientId == defender.clientId && CanUseDefense(session.b),
            session.b.clientId == attacker.clientId && CanUseSpecial(session.b),
            Mathf.CeilToInt(session.b.basicCooldownRemaining),
            Mathf.CeilToInt(session.b.specialCooldownRemaining),
            session.b.defenseUsesRemaining,
            statusMessage,
            BuildTargetParams(session.b.clientId)
        );
    }

    private void TickCooldowns(CombatSession session, float deltaTime)
    {
        TickCombatantCooldowns(session.a, deltaTime);
        TickCombatantCooldowns(session.b, deltaTime);
    }

    private void TickCombatantCooldowns(CombatantState combatant, float deltaTime)
    {
        combatant.basicCooldownRemaining = Mathf.Max(0f, combatant.basicCooldownRemaining - deltaTime);
        combatant.specialCooldownRemaining = Mathf.Max(0f, combatant.specialCooldownRemaining - deltaTime);
    }

    private void AutoResolveTurn(CombatSession session)
    {
        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);

        if (CanUseBasic(attacker))
        {
            ResolveAttack(session, attacker, defender, attacker.plant.basicAttack, false);
            return;
        }

        if (CanUseSpecial(attacker))
        {
            ResolveAttack(session, attacker, defender, attacker.plant.specialSkill, true);
            return;
        }

        AdvanceTurn(session, "Tiempo agotado");
    }

    private void ResolveAttack(CombatSession session, CombatantState attacker, CombatantState defender, AbilityData ability, bool isSpecial)
    {
        if (ability == null)
        {
            AdvanceTurn(session, "No hay habilidad configurada");
            return;
        }

        int damage = attacker.plant.baseAttack + ability.power;

        bool hasBiomeBonus =
            session.combatBiome != PlantBiomeType.Templado &&
            attacker.plant.biomeType == session.combatBiome;

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

        if (ability.heals)
        {
            int healAmount = attacker.plant.baseDefense + ability.healValue;
            attacker.currentHP = Mathf.Min(attacker.maxHP, attacker.currentHP + healAmount);
        }

        if (ability.buffsAttack)
        {
            attacker.attackBuffPercent = ability.attackBuffPercent;
            attacker.attackBuffTurnsRemaining = Mathf.Max(1, ability.buffDurationTurns);
        }

        if (ability.disablesShield)
            defender.shieldDisabledTurnsRemaining = Mathf.Max(1, ability.disablesShieldDurationTurns);

        if (isSpecial)
            attacker.specialCooldownRemaining = Mathf.Max(0f, ability.cooldownSeconds);
        else
            attacker.basicCooldownRemaining = Mathf.Max(0f, ability.cooldownSeconds);

        if (attacker.attackBuffTurnsRemaining > 0)
            attacker.attackBuffTurnsRemaining--;

        if (defender.currentHP <= 0)
        {
            StartCoroutine(FinishCombatRoutine(session, attacker, defender));
            return;
        }

        if (ability.stealsTurn)
        {
            session.turnTimerRemaining = turnDurationSeconds;
            session.lastBroadcastedSecond = -1;
            BroadcastSessionState(session, $"{attacker.plant.displayName} usó {ability.displayName} y conserva el turno");
            return;
        }

        AdvanceTurn(session, $"{attacker.plant.displayName} usó {ability.displayName}");
    }

    private void ArmDefense(CombatantState defender)
    {
        AbilityData defense = defender.plant.defenseSkill;
        if (defense == null)
            return;

        defender.defenseUsesRemaining = Mathf.Max(0, defender.defenseUsesRemaining - 1);
        defender.defenseArmed = true;

        defender.armedShieldValue = defense.grantsShield
            ? defender.plant.baseDefense + defense.shieldValue
            : 0;

        defender.armedDamageReduction = defense.reducesIncomingDamage
            ? defense.damageReductionPercent
            : 0f;
    }

    private void AdvanceTurn(CombatSession session, string statusMessage)
    {
        session.attackerId = GetDefender(session).clientId;
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
        return combatant.plant != null
            && combatant.plant.basicAttack != null
            && combatant.basicCooldownRemaining <= 0f;
    }

    private bool CanUseSpecial(CombatantState combatant)
    {
        return combatant.plant != null
            && combatant.plant.specialSkill != null
            && combatant.specialCooldownRemaining <= 0f;
    }

    private bool CanUseDefense(CombatantState combatant)
    {
        return combatant.plant != null
            && combatant.plant.defenseSkill != null
            && !combatant.defenseArmed
            && combatant.defenseUsesRemaining > 0
            && combatant.shieldDisabledTurnsRemaining <= 0;
    }

    private CombatantState GetAttacker(CombatSession session)
    {
        return session.a.clientId == session.attackerId ? session.a : session.b;
    }

    private CombatantState GetDefender(CombatSession session)
    {
        return session.a.clientId == session.attackerId ? session.b : session.a;
    }

    private string GetTurnStatusMessage(CombatSession session)
    {
        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);

        return $"{attacker.plant.displayName} ataca - {defender.plant.displayName} puede defender";
    }

    private IEnumerator FinishCombatRoutine(CombatSession session, CombatantState winner, CombatantState loser)
    {
        session.finished = true;

        int winnerXP = (session.combatBiome != PlantBiomeType.Templado && winner.plant.biomeType == session.combatBiome)
            ? winner.plant.xpWinBiomeBonus
            : winner.plant.xpWin;

        int loserXP = (session.combatBiome != PlantBiomeType.Templado && loser.plant.biomeType == session.combatBiome)
            ? loser.plant.xpLoseBiomeBonus
            : loser.plant.xpLose;

        winner.loadout.AddXP(winnerXP);
        loser.loadout.AddXP(loserXP);

        session.a.bridge.UpdateCombatUIClientRpc(
            session.a.currentHP,
            session.a.maxHP,
            session.b.currentHP,
            session.b.maxHP,
            0,
            false, false, false,
            Mathf.CeilToInt(session.a.basicCooldownRemaining),
            Mathf.CeilToInt(session.a.specialCooldownRemaining),
            session.a.defenseUsesRemaining,
            session.a.clientId == winner.clientId ? "Ganaste" : "Perdiste",
            BuildTargetParams(session.a.clientId)
        );

        session.b.bridge.UpdateCombatUIClientRpc(
            session.b.currentHP,
            session.b.maxHP,
            session.a.currentHP,
            session.a.maxHP,
            0,
            false, false, false,
            Mathf.CeilToInt(session.b.basicCooldownRemaining),
            Mathf.CeilToInt(session.b.specialCooldownRemaining),
            session.b.defenseUsesRemaining,
            session.b.clientId == winner.clientId ? "Ganaste" : "Perdiste",
            BuildTargetParams(session.b.clientId)
        );

        yield return new WaitForSeconds(postFinishDelaySeconds);

        session.a.bridge.HideCombatUIClientRpc(BuildTargetParams(session.a.clientId));
        session.b.bridge.HideCombatUIClientRpc(BuildTargetParams(session.b.clientId));

        RemoveSessionSilently(session.duelId);

        DuelArenaManager.Instance?.EndDuel(session.duelId);
    }

    private static ClientRpcParams BuildTargetParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }
}