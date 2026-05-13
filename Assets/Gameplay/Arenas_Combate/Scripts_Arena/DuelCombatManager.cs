using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public partial class DuelCombatManager : NetworkBehaviour
{
    public static DuelCombatManager Instance { get; private set; }

    [SerializeField] private float turnDurationSeconds = 5f;
    [SerializeField] private float postFinishDelaySeconds = 2f;

    [SerializeField] private float biomeDamageBonusPercent = 10f;

    [Header("Recompensas")]
    [SerializeField] private PlantDataBase plantDataBase;
    [SerializeField] private bool rewardPlantByCombatBiome = true;
    [SerializeField] private bool grantPlantRewardOnPlayerVictory = true;

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
        public BossCombatData bossData;
        public string displayName;
        public PlantBiomeType biomeType;

        public int maxHP;
        public int currentHP;
        public int baseAttack;
        public int baseDefense;
        public int baseSpecialAttack;
        public float defensePercent;

        public string basicAttackName;
        public string defenseName;
        public string specialAttackName;

        public GameObject basicAttackVfxPrefab;
        public GameObject defenseVfxPrefab;
        public GameObject specialAttackVfxPrefab;
        public GameObject impactVfxPrefab;

        public bool basicVfxTravelsToTarget;
        public bool defenseVfxTravelsToTarget;
        public bool specialVfxTravelsToTarget;

        public float basicVfxMoveSpeed;
        public float defenseVfxMoveSpeed;
        public float specialVfxMoveSpeed;

        public float basicVfxLifetime;
        public float defenseVfxLifetime;
        public float specialVfxLifetime;

        public bool orientAttackVfxToTarget;
        public bool orientDefenseVfxToTarget;

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
                PlayCombatVfxForAction(session, defender, defender, CombatActionType.Defense);
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
            defensePercent = plant.defensePercent,

            basicAttackName = plant.GetBasicAttackName(),
            defenseName = plant.GetDefenseName(),
            specialAttackName = plant.GetSpecialName(),

            basicAttackVfxPrefab = plant.basicAttack != null ? plant.basicAttack.vfxPrefab : null,
            defenseVfxPrefab = plant.defenseSkill != null ? plant.defenseSkill.vfxPrefab : null,
            specialAttackVfxPrefab = plant.specialSkill != null ? plant.specialSkill.vfxPrefab : null,

            impactVfxPrefab = plant.basicAttack != null && plant.basicAttack.impactVfxPrefab != null
                ? plant.basicAttack.impactVfxPrefab
                : plant.specialSkill != null ? plant.specialSkill.impactVfxPrefab : null,

            basicVfxTravelsToTarget = plant.basicAttack == null || plant.basicAttack.vfxTravelsToTarget,
            defenseVfxTravelsToTarget = plant.defenseSkill != null && plant.defenseSkill.vfxTravelsToTarget,
            specialVfxTravelsToTarget = plant.specialSkill == null || plant.specialSkill.vfxTravelsToTarget,

            basicVfxMoveSpeed = plant.basicAttack != null ? plant.basicAttack.vfxMoveSpeed : 12f,
            defenseVfxMoveSpeed = plant.defenseSkill != null ? plant.defenseSkill.vfxMoveSpeed : 0f,
            specialVfxMoveSpeed = plant.specialSkill != null ? plant.specialSkill.vfxMoveSpeed : 9f,

            basicVfxLifetime = plant.basicAttack != null ? plant.basicAttack.vfxLifetime : 5f,
            defenseVfxLifetime = plant.defenseSkill != null ? plant.defenseSkill.vfxLifetime : 2.5f,
            specialVfxLifetime = plant.specialSkill != null ? plant.specialSkill.vfxLifetime : 5f,

            orientAttackVfxToTarget = plant.basicAttack == null || plant.basicAttack.orientVfxToTarget,
            orientDefenseVfxToTarget = plant.defenseSkill != null && plant.defenseSkill.orientVfxToTarget,

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

        BossCombatData bossData = bossNetworkObject.GetComponentInChildren<BossCombatData>(true);

        PlantBiomeType bossBiome = bossData != null ? bossData.BossBiome : bossController.BossBiome;
        string bossName = bossData != null && !string.IsNullOrWhiteSpace(bossData.BossDisplayName)
            ? bossData.BossDisplayName
            : string.IsNullOrWhiteSpace(bossController.BossDisplayName) ? "Boss" : bossController.BossDisplayName;

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

        if (bossData != null)
        {
            maxHp = bossData.MaxHP;
            basicDamage = bossData.BasicAttackDamage;
            specialDamage = bossData.SpecialAttackDamage;
            defenseValue = bossData.DefenseValue;
            defensePercent = bossData.DefensePercent;
            basicName = bossData.BasicAttackName;
            defenseName = bossData.DefenseName;
            specialName = bossData.SpecialAttackName;
        }

        return new CombatantState
        {
            isBoss = true,
            clientId = ulong.MaxValue,
            bossNetworkObject = bossNetworkObject,
            bossController = bossController,
            bossData = bossData,
            displayName = bossName,
            biomeType = bossBiome,

            maxHP = Mathf.Max(1, maxHp),
            currentHP = Mathf.Max(1, maxHp),
            baseAttack = Mathf.Max(1, basicDamage),
            baseDefense = Mathf.Max(0, defenseValue),
            baseSpecialAttack = Mathf.Max(1, specialDamage),
            defensePercent = defensePercent,

            basicAttackName = basicName,
            defenseName = defenseName,
            specialAttackName = specialName,

            basicAttackVfxPrefab = bossData != null ? bossData.BasicAttackVfxPrefab : null,
            defenseVfxPrefab = bossData != null ? bossData.DefenseVfxPrefab : null,
            specialAttackVfxPrefab = bossData != null ? bossData.SpecialAttackVfxPrefab : null,
            impactVfxPrefab = bossData != null ? bossData.ImpactVfxPrefab : null,

            basicVfxTravelsToTarget = bossData == null || bossData.BasicVfxTravelsToTarget,
            defenseVfxTravelsToTarget = bossData != null && bossData.DefenseVfxTravelsToTarget,
            specialVfxTravelsToTarget = bossData == null || bossData.SpecialVfxTravelsToTarget,

            basicVfxMoveSpeed = bossData != null ? bossData.BasicVfxMoveSpeed : 12f,
            defenseVfxMoveSpeed = bossData != null ? bossData.DefenseVfxMoveSpeed : 0f,
            specialVfxMoveSpeed = bossData != null ? bossData.SpecialVfxMoveSpeed : 9f,

            basicVfxLifetime = bossData != null ? bossData.BasicVfxLifetime : 5f,
            defenseVfxLifetime = bossData != null ? bossData.DefenseVfxLifetime : 2.5f,
            specialVfxLifetime = bossData != null ? bossData.SpecialVfxLifetime : 5f,

            orientAttackVfxToTarget = bossData == null || bossData.OrientAttackVfxToTarget,
            orientDefenseVfxToTarget = bossData != null && bossData.OrientDefenseVfxToTarget,

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