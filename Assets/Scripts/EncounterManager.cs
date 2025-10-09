using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls which enemies/regions the player encounters, in order.
/// Assign the 5 RegionData assets in Inspector (region1..region5).
/// </summary>
public class EncounterManager : MonoBehaviour
{
    [Header("Region Setup (manually assigned in Inspector)")]
    public RegionData region1; // Encounters 1–10
    public RegionData region2; // Encounters 11–20
    public RegionData region3; // Encounters 21–30
    public RegionData region4; // Encounters 31–40
    public RegionData region5; // Encounters 41–50

    [Header("Encounter Control")]
    public int currentEncounter = 1;
    public int maxEncounters = 50;

    [Header("Starting Team (assign in inspector for first run)")]
    [Tooltip("Initial player roster used when no persistent data exists.")]
    public List<CharacterData> startingTeam = new List<CharacterData>();

    [Header("Battle Reference")]
    public BattleManager battleManager;

    private void Start()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (battleManager == null)
        {
            Debug.LogError("❌ EncounterManager: No BattleManager found in the scene!");
            return;
        }

        if (region1 == null && region2 == null && region3 == null && region4 == null && region5 == null)
        {
            Debug.LogError("❌ EncounterManager: No regions assigned (region1..region5)!");
            return;
        }

        StartNextEncounter();
    }

    public void StartNextEncounter()
    {
        if (currentEncounter > maxEncounters)
        {
            Debug.Log("🎉 All encounters completed!");
            return;
        }

        RegionData currentRegion = GetRegionForEncounter(currentEncounter);

        if (currentRegion == null)
        {
            Debug.LogError($"❌ No region data available for encounter {currentEncounter}");
            return;
        }

        Debug.Log($"🌍 Starting Encounter {currentEncounter} in {currentRegion.regionName}");

        // Build player team from persistent data if available, otherwise use startingTeam
        var playerTeamData = new List<CharacterData>();

        if (PersistentPlayerData.Instance != null)
        {
            var savedRuntimes = PersistentPlayerData.Instance.GetAllPlayerRuntimes();
            if (savedRuntimes != null && savedRuntimes.Count > 0)
            {
                foreach (var rt in savedRuntimes)
                {
                    if (rt?.baseData != null)
                        playerTeamData.Add(rt.baseData);
                }
            }
        }

        if (playerTeamData.Count == 0)
        {
            // Fallback to inspector-provided starting team
            if (startingTeam != null && startingTeam.Count > 0)
                playerTeamData.AddRange(startingTeam);
            else
                Debug.LogWarning("⚠️ EncounterManager: No startingTeam assigned and no persisted team found.");
        }

        // Generate enemies for this encounter
        List<CharacterData> enemiesToSpawn = GenerateEnemyTeam(currentRegion);

        // Start the battle
        battleManager.StartBattle(playerTeamData, enemiesToSpawn);
        Debug.Log($"⚔️ Encounter {currentEncounter} started in {currentRegion.regionName}");
    }

    private RegionData GetRegionForEncounter(int encounterNumber)
    {
        if (encounterNumber <= 10) return region1;
        if (encounterNumber <= 20) return region2;
        if (encounterNumber <= 30) return region3;
        if (encounterNumber <= 40) return region4;
        if (encounterNumber <= 50) return region5;
        return null;
    }

    private List<CharacterData> GenerateEnemyTeam(RegionData region)
    {
        var team = new List<CharacterData>();

        // Every 10th encounter → boss
        if (currentEncounter % 10 == 0 && region.bossEnemy != null)
        {
            Debug.Log($"👑 Boss Encounter! {region.bossEnemy.characterName}");
            team.Add(region.bossEnemy);
        }
        else
        {
            // Regular enemies — pick random 1–3 from region list
            if (region.possibleEnemies == null || region.possibleEnemies.Count == 0)
            {
                Debug.LogWarning($"⚠️ Region {region.regionName} has no possibleEnemies assigned!");
                return team;
            }

            int enemyCount = Random.Range(1, 4);
            for (int i = 0; i < enemyCount; i++)
            {
                var randomEnemy = region.possibleEnemies[Random.Range(0, region.possibleEnemies.Count)];
                team.Add(randomEnemy);
            }
        }

        return team;
    }

    public void EndEncounter()
    {
        Debug.Log($"✅ Encounter {currentEncounter} complete!");
        currentEncounter++;
        // small delay before starting next encounter
        Invoke(nameof(StartNextEncounter), 1.2f);
    }
}
