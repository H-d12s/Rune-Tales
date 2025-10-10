using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class EncounterManager : MonoBehaviour
{
    [Header("Region Setup (manually assigned in Inspector)")]
    public RegionData region1;
    public RegionData region2;
    public RegionData region3;
    public RegionData region4;
    public RegionData region5;

    [Header("Encounter Control")]
    public int currentEncounter = 1;
    public int maxEncounters = 50;

    [Header("Starting Team (assign in inspector for first run)")]
    public List<CharacterData> startingTeam = new List<CharacterData>();

    [Header("Recruitment Settings")]
    public List<CharacterData> recruitableCharacters = new List<CharacterData>();
    public int encountersBeforeRecruitment = 10;

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
            Debug.LogError("❌ EncounterManager: No regions assigned!");
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
            Debug.LogError($"❌ No region data for encounter {currentEncounter}");
            return;
        }

        Debug.Log($"🌍 Starting Encounter {currentEncounter} in {currentRegion.regionName}");

        var playerTeamData = LoadPlayerTeam();
        List<CharacterData> enemiesToSpawn = GenerateEnemyTeam(currentRegion);

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

        if (region.possibleEnemies == null || region.possibleEnemies.Count == 0)
        {
            Debug.LogWarning($"⚠️ Region {region.regionName} has no possibleEnemies assigned!");
            return team;
        }

        bool isBossEncounter = currentEncounter % encountersBeforeRecruitment == 0;
        if (isBossEncounter && region.bossEnemy != null)
        {
            Debug.Log($"👑 Boss encounter: {region.bossEnemy.characterName}");
            team.Add(region.bossEnemy);
            return team;
        }

        int enemyCount = Random.Range(1, 4);
        for (int i = 0; i < enemyCount; i++)
        {
            var randomEnemy = region.possibleEnemies[Random.Range(0, region.possibleEnemies.Count)];
            team.Add(randomEnemy);
        }

        return team;
    }

    public void EndEncounter()
    {
        Debug.Log($"✅ Encounter {currentEncounter} complete!");
        StartCoroutine(WaitUntilMoveLearningDone());
    }

    private IEnumerator WaitUntilMoveLearningDone()
    {
        var expSystem = FindFirstObjectByType<ExperienceSystem>();
        if (expSystem != null)
        {
            while (expSystem.moveLearningInProgress)
            {
                yield return null;
            }
        }

        if (currentEncounter % encountersBeforeRecruitment == 0)
        {
            Debug.Log("👑 Boss defeated! Starting recruitment phase...");
            StartCoroutine(StartRecruitmentEncounterAfterBoss());
        }
        else
        {
            currentEncounter++;
            Invoke(nameof(StartNextEncounter), 1.2f);
        }
    }

    private IEnumerator StartRecruitmentEncounterAfterBoss()
    {
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(StartRecruitmentEncounter());
        currentEncounter++;
        Invoke(nameof(StartNextEncounter), 1.2f);
    }

    private List<CharacterData> LoadPlayerTeam()
    {
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
            if (startingTeam != null && startingTeam.Count > 0)
                playerTeamData.AddRange(startingTeam);
            else
                Debug.LogWarning("⚠️ EncounterManager: No startingTeam assigned and no persisted team found.");
        }

        return playerTeamData;
    }

    private IEnumerator StartRecruitmentEncounter()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("✨ Recruitment encounter triggered!");

        if (recruitableCharacters == null || recruitableCharacters.Count == 0)
        {
            Debug.LogWarning("⚠️ No recruitable characters set in EncounterManager!");
            yield break;
        }

        var playerRuntimes = PersistentPlayerData.Instance.GetAllPlayerRuntimes();
        List<string> ownedNames = new List<string>();
        foreach (var r in playerRuntimes)
            ownedNames.Add(r.baseData.characterName);

        var candidates = recruitableCharacters.FindAll(c => !ownedNames.Contains(c.characterName));

        if (candidates.Count == 0)
        {
            Debug.Log("No new recruits available!");
            yield break;
        }

        var recruitData = candidates[Random.Range(0, candidates.Count)];
        Debug.Log($"🎉 A recruitable hero appears: {recruitData.characterName}");

        if (battleManager != null)
        {
            var playerTeamData = LoadPlayerTeam();
            battleManager.StartRecruitmentBattle(playerTeamData, recruitData);

            yield return new WaitUntil(() => battleManager.recruitmentComplete == true);
            Debug.Log("✨ Recruitment encounter finished.");
        }
        else
        {
            Debug.LogError("❌ No BattleManager found to start recruitment battle!");
        }
    }
}
