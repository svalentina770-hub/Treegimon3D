using System.Collections;
using UnityEngine;

public partial class DuelCombatManager
{
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

        string rewardPlantName = string.Empty;

        if (!winner.isBoss && winner.loadout != null)
        {
            winner.loadout.AddXP(winnerXP);
            TryGrantPlantReward(session, winner, out rewardPlantName);
        }

        if (!loser.isBoss && loser.loadout != null)
            loser.loadout.AddXP(loserXP);

        BroadcastFinishState(session, winner, rewardPlantName);

        yield return new WaitForSeconds(postFinishDelaySeconds);

        HideCombatUIForPlayer(session.a);
        HideCombatUIForPlayer(session.b);

        RemoveSessionSilently(session.duelId);

        DuelArenaManager.Instance?.EndDuel(session.duelId);
    }

    private bool TryGrantPlantReward(CombatSession session, CombatantState winner, out string rewardPlantName)
    {
        rewardPlantName = string.Empty;

        if (!grantPlantRewardOnPlayerVictory)
            return false;

        if (session == null || winner == null || winner.isBoss || winner.loadout == null)
            return false;

        PlantDataBase database = ResolvePlantDataBase();
        if (database == null)
        {
            Debug.LogWarning("DuelCombatManager: No se encontró PlantDataBase para otorgar recompensa.");
            return false;
        }

        PlantSpeciesData rewardSpecies = rewardPlantByCombatBiome
            ? database.GetRandomSpeciesByBiome(session.combatBiome)
            : database.GetRandomSpecies();

        if (rewardSpecies == null)
        {
            Debug.LogWarning("DuelCombatManager: No se pudo seleccionar una especie de recompensa.");
            return false;
        }

        PlantModelVariantData rewardVariant = database.GetRandomVariantForSpecies(rewardSpecies);

        if (!winner.loadout.TryAddRewardPlant(rewardSpecies, rewardVariant, out rewardPlantName))
        {
            Debug.LogWarning("DuelCombatManager: No se pudo guardar la planta recompensa en el archivo del usuario.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(rewardPlantName))
            rewardPlantName = string.IsNullOrWhiteSpace(rewardSpecies.displayName) ? rewardSpecies.plantId : rewardSpecies.displayName;

        return true;
    }

    private PlantDataBase ResolvePlantDataBase()
    {
        if (plantDataBase != null)
            return plantDataBase;

        plantDataBase = Resources.Load<PlantDataBase>("Data/PlantDataBase");

        if (plantDataBase == null)
            plantDataBase = Resources.Load<PlantDataBase>("PlantDataBase");

        return plantDataBase;
    }

    private void BroadcastFinishState(CombatSession session, CombatantState winner, string rewardPlantName)
    {
        BroadcastFinishForPlayer(session.a, session.b, winner, rewardPlantName);
        BroadcastFinishForPlayer(session.b, session.a, winner, rewardPlantName);
    }

    private void BroadcastFinishForPlayer(CombatantState viewer, CombatantState rival, CombatantState winner, string rewardPlantName)
    {
        if (viewer == null || viewer.isBoss || viewer.bridge == null)
            return;
        bool viewerWon = viewer == winner;
        string finishMessage = viewerWon ? "Ganaste" : "Perdiste";

        if (viewerWon && !string.IsNullOrWhiteSpace(rewardPlantName))
            finishMessage = $"Ganaste. Nueva planta obtenida: {rewardPlantName}";

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
            finishMessage,
            BuildTargetParams(viewer.clientId)
        );
    }

    private void HideCombatUIForPlayer(CombatantState combatant)
    {
        if (combatant == null || combatant.isBoss || combatant.bridge == null)
            return;

        combatant.bridge.HideCombatUIClientRpc(BuildTargetParams(combatant.clientId));
    }
}
