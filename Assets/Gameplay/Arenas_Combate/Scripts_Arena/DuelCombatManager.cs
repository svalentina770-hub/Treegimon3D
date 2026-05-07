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

    [Header("Boss IA")]
    [SerializeField] private int defaultBossMaxHP = 1500;
    [SerializeField] private int defaultBossBasicDamage = 100;
    [SerializeField] private int defaultBossSpecialDamage = 180;
    [SerializeField] private int defaultBossDefenseValue = 20;
    [SerializeField, Range(0f, 1f)] private float defaultBossDefensePercent = 0.25f;
    [SerializeField] private int bossDefenseUses = 2;
    [SerializeField] private int bossSpecialEveryTurns = 3;

    private readonly Dictionary<int, CombatSession> sessionsByDuelId = new();
    private readonly Dictionary<ulong, int> duelIdByPlayer = new();

    private class CombatantState
    {
        public bool isBoss;
        public ulong clientId;

        public PlayerCombatBridge bridge;
        public PlayerPlantLoadout loadout;
        public PlantSpeciesData plant;

        public NetworkObject bossNetworkObject;
        public BossZoneController bossController;
        public string displayName;
        public PlantBiomeType biomeType;

        public int maxHP;
        public int currentHP;
        public int baseAttack;
        public int baseDefense;
        public int baseSpecialAttack;

        public string basicAttackName;
        public string defenseName;
        public string specialAttackName;

        public float basicCooldownRemaining;
        public float specialCooldownRemaining;

        public int defenseUsesRemaining = 2;
        public bool defenseArmed;
        public int armedShieldValue;
        public float armedDamageReduction;

        public int attackBuffPercent;
        public int attackBuffTurnsRemaining;

        public int shieldDisabledTurnsRemaining;
        public int turnsTaken;
    }

    private class CombatSession
    {
        public int duelId;
        public CombatantState a;
        public CombatantState b;

        public bool attackerIsA;
        public float turnTimerRemaining;
        public int lastBroadcastedSecond = -1;
        public bool finished;
        public PlantBiomeType combatBiome;
        public bool isBossSession;
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
            if (session.finished)
                continue;

            TickCooldowns(session, Time.deltaTime);

            session.turnTimerRemaining -= Time.deltaTime;
            int displaySeconds = Mathf.Max(0, Mathf.CeilToInt(session.turnTimerRemaining));

            if (displaySeconds != session.lastBroadcastedSecond)
            {
                session.lastBroadcastedSecond = displaySeconds;
                BroadcastSessionState(session, GetTurnStatusMessage(session));
            }

            if (session.turnTimerRemaining <= 0f)
                AutoResolveTurn(session);
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
            attackerIsA = true,
            turnTimerRemaining = turnDurationSeconds,
            lastBroadcastedSecond = -1,
            finished = false,
            combatBiome = combatBiome,
            isBossSession = false
        };

        sessionsByDuelId[duelId] = session;
        duelIdByPlayer[challengerId] = duelId;
        duelIdByPlayer[challengedId] = duelId;

        ShowInitialUI(session);
        BroadcastSessionState(session, GetTurnStatusMessage(session));
    }

    public void StartBossCombatSession(int duelId, ulong playerId, NetworkObject bossNetworkObject, PlantBiomeType combatBiome)
    {
        if (sessionsByDuelId.ContainsKey(duelId))
            return;

        CombatantState player = BuildCombatant(playerId);
        CombatantState boss = BuildBossCombatant(bossNetworkObject);

        if (player == null || boss == null)
        {
            Debug.LogWarning("No se pudo iniciar sesión de combate contra boss. Faltan datos del jugador o del boss.");
            return;
        }

        CombatSession session = new CombatSession
        {
            duelId = duelId,
            a = player,
            b = boss,
            attackerIsA = true,
            turnTimerRemaining = turnDurationSeconds,
            lastBroadcastedSecond = -1,
            finished = false,
            combatBiome = combatBiome,
            isBossSession = true
        };

        sessionsByDuelId[duelId] = session;
        duelIdByPlayer[playerId] = duelId;

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

        if (attacker.isBoss)
            return;

        if (clientId == attacker.clientId)
        {
            if (actionType == CombatActionType.BasicAttack)
            {
                if (!CanUseBasic(attacker))
                    return;

                ResolveAttack(session, attacker, defender, attacker.plant != null ? attacker.plant.basicAttack : null, false);
                return;
            }

            if (actionType == CombatActionType.SpecialAttack)
            {
                if (!CanUseSpecial(attacker))
                    return;

                ResolveAttack(session, attacker, defender, attacker.plant != null ? attacker.plant.specialSkill : null, true);
                return;
            }

            return;
        }

        if (clientId == defender.clientId)
        {
            if (actionType == CombatActionType.Defense && CanUseDefense(defender))
            {
                ArmDefense(defender);
                BroadcastSessionState(session, $"{GetDisplayName(defender)} activó defensa");
            }
        }
    }

    public void RemoveSessionSilently(int duelId)
    {
        if (!sessionsByDuelId.TryGetValue(duelId, out CombatSession session))
            return;

        if (!session.a.isBoss)
            duelIdByPlayer.Remove(session.a.clientId);

        if (!session.b.isBoss)
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

        int hp = loadout.GetCurrentHp() > 0 ? loadout.GetCurrentHp() : plant.baseHP;

        return new CombatantState
        {
            isBoss = false,
            clientId = clientId,
            bridge = bridge,
            loadout = loadout,
            plant = plant,
            displayName = string.IsNullOrWhiteSpace(plant.displayName) ? plant.plantId : plant.displayName,
            biomeType = plant.biomeType,
            maxHP = Mathf.Max(1, plant.baseHP),
            currentHP = Mathf.Clamp(hp, 1, Mathf.Max(1, plant.baseHP)),
            baseAttack = Mathf.Max(1, plant.baseAttack),
            baseDefense = Mathf.Max(0, plant.baseDefense),
            baseSpecialAttack = Mathf.Max(1, plant.baseSpecialAttack),
            basicAttackName = plant.GetBasicAttackName(),
            defenseName = plant.GetDefenseName(),
            specialAttackName = plant.GetSpecialName(),
            basicCooldownRemaining = 0f,
            specialCooldownRemaining = 0f,
            defenseUsesRemaining = 2,
            defenseArmed = false,
            armedShieldValue = 0,
            armedDamageReduction = 0f,
            attackBuffPercent = 0,
            attackBuffTurnsRemaining = 0,
            shieldDisabledTurnsRemaining = 0,
            turnsTaken = 0
        };
    }

    private CombatantState BuildBossCombatant(NetworkObject bossNetworkObject)
    {
        if (bossNetworkObject == null)
            return null;

        BossZoneController bossController = bossNetworkObject.GetComponentInChildren<BossZoneController>(true);
        if (bossController == null)
            return null;

        PlantBiomeType bossBiome = bossController.BossBiome;
        string bossName = string.IsNullOrWhiteSpace(bossController.BossDisplayName)
            ? "Boss"
            : bossController.BossDisplayName;

        int maxHp = defaultBossMaxHP;
        int basicDamage = defaultBossBasicDamage;
        int specialDamage = defaultBossSpecialDamage;
        int defenseValue = defaultBossDefenseValue;
        float defensePercent = defaultBossDefensePercent;
        string basicName = "Ataque básico";
        string defenseName = "Defensa";
        string specialName = "Ataque especial";

        ApplyBossDefaultsByBiome(
            bossBiome,
            ref maxHp,
            ref basicDamage,
            ref specialDamage,
            ref defenseValue,
            ref defensePercent,
            ref basicName,
            ref defenseName,
            ref specialName
        );

        return new CombatantState
        {
            isBoss = true,
            clientId = ulong.MaxValue,
            bossNetworkObject = bossNetworkObject,
            bossController = bossController,
            displayName = bossName,
            biomeType = bossBiome,
            maxHP = Mathf.Max(1, maxHp),
            currentHP = Mathf.Max(1, maxHp),
            baseAttack = Mathf.Max(1, basicDamage),
            baseDefense = Mathf.Max(0, defenseValue),
            baseSpecialAttack = Mathf.Max(1, specialDamage),
            basicAttackName = basicName,
            defenseName = defenseName,
            specialAttackName = specialName,
            basicCooldownRemaining = 0f,
            specialCooldownRemaining = 0f,
            defenseUsesRemaining = bossDefenseUses,
            defenseArmed = false,
            armedShieldValue = 0,
            armedDamageReduction = 0f,
            attackBuffPercent = 0,
            attackBuffTurnsRemaining = 0,
            shieldDisabledTurnsRemaining = 0,
            turnsTaken = 0
        };
    }

    private void ApplyBossDefaultsByBiome(
        PlantBiomeType biome,
        ref int maxHp,
        ref int basicDamage,
        ref int specialDamage,
        ref int defenseValue,
        ref float defensePercent,
        ref string basicName,
        ref string defenseName,
        ref string specialName)
    {
        switch (biome)
        {
            case PlantBiomeType.Hidro:
                maxHp = 1400;
                basicDamage = 90;
                specialDamage = 160;
                defenseValue = 20;
                defensePercent = 0.25f;
                basicName = "Picotazo de agua";
                defenseName = "Plumas húmedas";
                specialName = "Oleaje del humedal";
                break;

            case PlantBiomeType.Solar:
                maxHp = 1500;
                basicDamage = 110;
                specialDamage = 190;
                defenseValue = 15;
                defensePercent = 0.20f;
                basicName = "Rebote solar";
                defenseName = "Brillo protector";
                specialName = "Explosión solar";
                break;

            case PlantBiomeType.Montana:
                maxHp = 1800;
                basicDamage = 100;
                specialDamage = 210;
                defenseValue = 30;
                defensePercent = 0.35f;
                basicName = "Embestida pesada";
                defenseName = "Blindaje de montaña";
                specialName = "Avalancha";
                break;

            case PlantBiomeType.Xerofito:
                maxHp = 1600;
                basicDamage = 100;
                specialDamage = 180;
                defenseValue = 25;
                defensePercent = 0.30f;
                basicName = "Lección espinosa";
                defenseName = "Estrategia académica";
                specialName = "Examen sorpresa";
                break;

            case PlantBiomeType.Templado:
                maxHp = 1450;
                basicDamage = 95;
                specialDamage = 170;
                defenseValue = 18;
                defensePercent = 0.25f;
                basicName = "Picada aérea";
                defenseName = "Alas protectoras";
                specialName = "Vuelo templado";
                break;

            case PlantBiomeType.Central:
                maxHp = 2200;
                basicDamage = 130;
                specialDamage = 260;
                defenseValue = 40;
                defensePercent = 0.40f;
                basicName = "Carga ecuestre";
                defenseName = "Honor del campus";
                specialName = "Juramento del centinela";
                break;
        }
    }

    private void ShowInitialUI(CombatSession session)
    {
        ShowInitialUIForPlayer(session.a, session.b);
        ShowInitialUIForPlayer(session.b, session.a);
    }

    private void ShowInitialUIForPlayer(CombatantState viewer, CombatantState rival)
    {
        if (viewer == null || viewer.isBoss || viewer.bridge == null)
            return;

        viewer.bridge.ShowCombatUIClientRpc(
            GetDisplayName(viewer),
            GetDisplayName(rival),
            viewer.currentHP,
            viewer.maxHP,
            rival.currentHP,
            rival.maxHP,
            viewer.basicAttackName,
            viewer.defenseName,
            viewer.specialAttackName,
            BuildTargetParams(viewer.clientId)
        );
    }

    private void BroadcastSessionState(CombatSession session, string statusMessage)
    {
        CombatantState attacker = GetAttacker(session);
        CombatantState defender = GetDefender(session);
        int secondsRemaining = Mathf.Max(0, Mathf.CeilToInt(session.turnTimerRemaining));

        BroadcastStateForPlayer(session.a, session.b, attacker, defender, secondsRemaining, statusMessage);
        BroadcastStateForPlayer(session.b, session.a, attacker, defender, secondsRemaining, statusMessage);
    }

    private void BroadcastStateForPlayer(
        CombatantState viewer,
        CombatantState rival,
        CombatantState attacker,
        CombatantState defender,
        int secondsRemaining,
        string statusMessage)
    {
        if (viewer == null || viewer.isBoss || viewer.bridge == null)
            return;

        viewer.bridge.UpdateCombatUIClientRpc(
            viewer.currentHP,
            viewer.maxHP,
            rival.currentHP,
            rival.maxHP,
            secondsRemaining,
            viewer == attacker && CanUseBasic(viewer),
            viewer == defender && CanUseDefense(viewer),
            viewer == attacker && CanUseSpecial(viewer),
            Mathf.CeilToInt(viewer.basicCooldownRemaining),
            Mathf.CeilToInt(viewer.specialCooldownRemaining),
            viewer.defenseUsesRemaining,
            statusMessage,
            BuildTargetParams(viewer.clientId)
        );
    }

    private void TickCooldowns(CombatSession session)
    {
        TickCombatantCooldowns(session.a, Time.deltaTime);
        TickCombatantCooldowns(session.b, Time.deltaTime);
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

        bool shouldUseDefense = boss.currentHP < boss.maxHP * 0.35f && CanUseDefense(boss);
        if (shouldUseDefense)
        {
            ArmDefense(boss);
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
            defender.armedDamageReduction = defaultBossDefensePercent;
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

    private IEnumerator FinishCombatRoutine(CombatSession session, CombatantState winner, CombatantState loser)
    {
        session.finished = true;

        int winnerXP = 0;
        int loserXP = 0;

        if (!winner.isBoss && winner.plant != null)
        {
            winnerXP = (session.combatBiome != PlantBiomeType.Templado && winner.plant.biomeType == session.combatBiome)
                ? winner.plant.xpWinBiomeBonus
                : winner.plant.xpWin;
        }

        if (!loser.isBoss && loser.plant != null)
        {
            loserXP = (session.combatBiome != PlantBiomeType.Templado && loser.plant.biomeType == session.combatBiome)
                ? loser.plant.xpLoseBiomeBonus
                : loser.plant.xpLose;
        }

        if (!winner.isBoss && winner.loadout != null)
            winner.loadout.AddXP(winnerXP);

        if (!loser.isBoss && loser.loadout != null)
            loser.loadout.AddXP(loserXP);

        BroadcastFinishState(session, winner);

        yield return new WaitForSeconds(postFinishDelaySeconds);

        HideCombatUIForPlayer(session.a);
        HideCombatUIForPlayer(session.b);

        RemoveSessionSilently(session.duelId);

        DuelArenaManager.Instance?.EndDuel(session.duelId);
    }

    private void BroadcastFinishState(CombatSession session, CombatantState winner)
    {
        BroadcastFinishForPlayer(session.a, session.b, winner);
        BroadcastFinishForPlayer(session.b, session.a, winner);
    }

    private void BroadcastFinishForPlayer(CombatantState viewer, CombatantState rival, CombatantState winner)
    {
        if (viewer == null || viewer.isBoss || viewer.bridge == null)
            return;

        viewer.bridge.UpdateCombatUIClientRpc(
            viewer.currentHP,
            viewer.maxHP,
            rival.currentHP,
            rival.maxHP,
            0,
            false, false, false,
            Mathf.CeilToInt(viewer.basicCooldownRemaining),
            Mathf.CeilToInt(viewer.specialCooldownRemaining),
            viewer.defenseUsesRemaining,
            viewer == winner ? "Ganaste" : "Perdiste",
            BuildTargetParams(viewer.clientId)
        );
    }

    private void HideCombatUIForPlayer(CombatantState combatant)
    {
        if (combatant == null || combatant.isBoss || combatant.bridge == null)
            return;

        combatant.bridge.HideCombatUIClientRpc(BuildTargetParams(combatant.clientId));
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