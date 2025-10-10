using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class BattleManager : MonoBehaviour
{
    [Header("Visuals")]
    public Image backgroundImageUI;       // assign from Canvas
    private RegionData currentRegion;     // stores current region data

    private List<CharacterBattleController> turnOrder = new List<CharacterBattleController>();
    private bool battleActive = false;

    private ExperienceSystem expSystem;
    private BattleUIManager uiManager;
    private EncounterManager encounterManager;

    [Header("Spawn Points")]
    public Transform[] playerSpawnPoints;
    public Transform[] enemySpawnPoints;

    [Header("Prefabs")]
    public GameObject characterPrefab;

    [Header("Teams (Populated Dynamically)")]
    public List<CharacterData> playerTeam = new List<CharacterData>();
    public List<CharacterData> enemyTeam = new List<CharacterData>();

    private List<CharacterBattleController> playerControllers = new List<CharacterBattleController>();
    private List<CharacterBattleController> enemyControllers = new List<CharacterBattleController>();

    private int playerChoiceIndex = 0;

    private Dictionary<CharacterBattleController, (AttackData, CharacterBattleController)> chosenActions =
        new Dictionary<CharacterBattleController, (AttackData, CharacterBattleController)>();

    [Header("Spawn Offset")]
    public float verticalOffset = -1.5f;

    // -------------------------
    // Recruitment fields
    // -------------------------
    [Header("Recruitment")]
    [HideInInspector] public bool isRecruitmentBattle = false;
    [HideInInspector] public bool recruitmentComplete = false; // used by EncounterManager to wait
    private CharacterBattleController recruitTarget = null;
    public int maxPersuadeAttempts = 3;
    private int persuadeAttempts = 0;

    // ==========================================================
    // Called from EncounterManager
    // ==========================================================
    public void StartBattle(List<CharacterData> playerTeamData, List<CharacterData> enemyTeamData, RegionData region = null)
    {
        Debug.Log("⚔️ Starting new battle via EncounterManager...");

        CleanupOldInstances();

        // Reset
        StopAllCoroutines();
        battleActive = false;
        chosenActions.Clear();
        playerControllers.Clear();
        enemyControllers.Clear();

        playerTeam = playerTeamData;
        enemyTeam = enemyTeamData;
        currentRegion = region;

        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();
        encounterManager = FindFirstObjectByType<EncounterManager>();

        if (uiManager == null) Debug.LogError("❌ No BattleUIManager found!");
        if (expSystem == null) Debug.LogError("❌ No ExperienceSystem found!");
        if (encounterManager == null) Debug.LogError("❌ No EncounterManager found!");

        // Fade background if we have region data
        if (currentRegion != null)
        {
            Debug.Log($"🌄 Starting battle in region: {currentRegion.regionName}");
            StartCoroutine(FadeRegionBackground(currentRegion));
        }

        // Clean old spawned objects
        ClearSpawnedCharacters();

        // Spawn teams
        SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
        SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

        // Recruitment setup
        if (isRecruitmentBattle)
        {
            recruitTarget = enemyControllers.Count > 0 ? enemyControllers[0] : null;
            persuadeAttempts = 0;
            recruitmentComplete = false;
            if (uiManager != null) uiManager.SetPersuadeButtonActive(true);
            Debug.Log("🎯 Recruitment battle started.");
        }
        else
        {
            if (uiManager != null) uiManager.SetPersuadeButtonActive(false);
        }

        // Initialize EXP
        if (expSystem != null) expSystem.Initialize(playerControllers);

        // Start battle loop
        battleActive = true;
        StartCoroutine(BattleLoop());

        Debug.Log($"✅ Battle started: {playerControllers.Count} players vs {enemyControllers.Count} enemies");
    }

    public void StartRecruitmentBattle(List<CharacterData> playerTeamData, CharacterData recruitData)
    {
        if (recruitData == null)
        {
            Debug.LogError("❌ StartRecruitmentBattle called with null recruitData!");
            return;
        }

        isRecruitmentBattle = true;
        recruitmentComplete = false;
        var enemyList = new List<CharacterData> { recruitData };
        StartBattle(playerTeamData, enemyList);
    }

    public List<CharacterBattleController> GetAllEnemies()
    {
        return new List<CharacterBattleController>(enemyControllers);
    }

    private void ClearSpawnedCharacters()
    {
        if (playerSpawnPoints != null)
        {
            foreach (Transform t in playerSpawnPoints)
                for (int i = t.childCount - 1; i >= 0; i--)
                    Destroy(t.GetChild(i).gameObject);
        }

        if (enemySpawnPoints != null)
        {
            foreach (Transform t in enemySpawnPoints)
                for (int i = t.childCount - 1; i >= 0; i--)
                    Destroy(t.GetChild(i).gameObject);
        }

        playerControllers.Clear();
        enemyControllers.Clear();
    }

    private void SpawnTeam(List<CharacterData> teamData, Transform[] spawnPoints, List<CharacterBattleController> list, bool isPlayer)
    {
        if (teamData == null || spawnPoints == null) return;

        for (int i = 0; i < teamData.Count && i < spawnPoints.Length; i++)
        {
            var obj = Instantiate(characterPrefab, spawnPoints[i].position + new Vector3(0, verticalOffset, 0), Quaternion.identity);
            var ctrl = obj.GetComponent<CharacterBattleController>();

            ctrl.characterData = teamData[i];
            ctrl.isPlayer = isPlayer;
            ctrl.InitializeCharacter();

            // Apply persistent runtime BEFORE battle starts
            if (isPlayer && PersistentPlayerData.Instance != null)
            {
                PersistentPlayerData.Instance.ApplyToRuntime(ctrl.GetRuntimeCharacter());
            }

            // Flip enemy visuals
            if (!isPlayer)
            {
                var scale = obj.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * -1;
                obj.transform.localScale = scale;
            }

            list.Add(ctrl);
        }
    }

    // ==========================================================
    // Main loop
    // ==========================================================
    private IEnumerator BattleLoop()
    {
        while (battleActive)
        {
            // Player turn / choose actions
            yield return StartCoroutine(PlayerCommandPhase());

            // Enemy actions decided
            EnemyCommandPhase();

            // Execute actions
            yield return StartCoroutine(ResolveActions());

            // Check win/lose
            if (AreAllDead(enemyControllers))
            {
                StartCoroutine(HandleVictory(enemyControllers));
                yield break;
            }

            if (AreAllDead(playerControllers))
            {
                Debug.Log("💀 All players fainted! You lost...");
                if (PersistentPlayerData.Instance != null)
                    PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
                battleActive = false;
                yield break;
            }

            chosenActions.Clear();
            playerChoiceIndex = 0;
        }
    }

    // ==========================================================
    // Player decision phase — fixed: only active character can Persuade,
    // and we clear callbacks after each player's choice so actions do not persist.
    // ==========================================================
    private IEnumerator PlayerCommandPhase()
    {
        chosenActions.Clear();
        playerChoiceIndex = 0;

        while (playerChoiceIndex < playerControllers.Count)
        {
            var currentPlayer = playerControllers[playerChoiceIndex];
            var runtime = currentPlayer.GetRuntimeCharacter();

            if (!runtime.IsAlive)
            {
                playerChoiceIndex++;
                continue;
            }

            bool actionChosen = false;
            AttackData chosenAttack = null;
            CharacterBattleController chosenTarget = null;

            // Set UI to the active player
            if (uiManager != null)
            {
                uiManager.playerController = currentPlayer;
                uiManager.ShowMainActions();
            }

            // Assign attack callback (UI will invoke on attack confirm)
            if (uiManager != null)
            {
                uiManager.onAttackConfirmed = (attack, target) =>
                {
                    chosenAttack = attack;
                    chosenTarget = target;
                    actionChosen = true;
                };

                // Assign persuade callback for this specific player only:
                uiManager.onPersuadeRequested = () =>
                {
                    // find the first alive enemy (or the recruitTarget for recruitment battles)
                    CharacterBattleController target = null;
                    if (isRecruitmentBattle && recruitTarget != null && recruitTarget.GetRuntimeCharacter().IsAlive)
                    {
                        target = recruitTarget;
                    }
                    else
                    {
                        var enemiesAlive = enemyControllers.FindAll(e => e != null && e.GetRuntimeCharacter().IsAlive);
                        if (enemiesAlive.Count > 0) target = enemiesAlive[0];
                    }

                    if (target != null)
                    {
                        Debug.Log($"🗣️ {currentPlayer.characterData.characterName} attempting to persuade {target.characterData.characterName}...");
                        // Call TryPersuade on the recruit target — this increments attempt and handles result.
                        TryPersuade(target);
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ No valid persuasion target at this time.");
                    }

                    // Persuade ends the player's turn immediately (like throwing a pokeball)
                    actionChosen = true;
                };
            }

            // Wait until player picks an action (attack or persuade)
            yield return new WaitUntil(() => actionChosen);

            // If Attack chosen, store the action to resolve later
            if (chosenAttack != null && chosenTarget != null)
            {
                chosenActions[currentPlayer] = (chosenAttack, chosenTarget);
            }
            // If the player used Persuade, we marked actionChosen but we DO NOT queue an attack — turn ends.

            // Clear UI callbacks immediately so they can't be reused by the next player's turn
            if (uiManager != null)
            {
                uiManager.onAttackConfirmed = null;
                uiManager.onPersuadeRequested = null;
                uiManager.HideAll();
            }

            playerChoiceIndex++;
        }

        yield return new WaitForSeconds(0.2f);
    }

    // ==========================================================
    // Enemy decision
    // ==========================================================
    private void EnemyCommandPhase()
    {
        foreach (var enemy in enemyControllers)
        {
            var runtime = enemy.GetRuntimeCharacter();
            if (!runtime.IsAlive) continue;

            var attacks = runtime.equippedAttacks;
            if (attacks.Count == 0) continue;

            var attack = attacks[Random.Range(0, attacks.Count)];
            var targets = playerControllers.FindAll(p => p.GetRuntimeCharacter().IsAlive);
            if (targets.Count == 0) continue;

            var target = targets[Random.Range(0, targets.Count)];
            chosenActions[enemy] = (attack, target);
        }
    }

    // ==========================================================
    // Resolve actions ordered by speed
    // ==========================================================
    private IEnumerator ResolveActions()
    {
        turnOrder = new List<CharacterBattleController>(chosenActions.Keys);
        turnOrder.Sort((a, b) => b.GetRuntimeCharacter().Speed.CompareTo(a.GetRuntimeCharacter().Speed));

        foreach (var actor in turnOrder)
        {
            if (!actor.GetRuntimeCharacter().IsAlive) continue;
            if (!chosenActions.ContainsKey(actor)) continue;

            var (attack, target) = chosenActions[actor];
            if (target == null || !target.GetRuntimeCharacter().IsAlive) continue;

            PerformAttack(actor, target, attack);
            yield return new WaitForSeconds(1.5f);
        }
    }

    // ==========================================================
    // Attack execution
    // ==========================================================
    public void PerformAttack(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
    {
        if (attacker == null || target == null || attack == null) return;

        var attackerName = attacker.characterData.characterName;
        var targetName = target.characterData.characterName;
        var attackerRuntime = attacker.GetRuntimeCharacter();
        var targetRuntime = target.GetRuntimeCharacter();

        int dice = Random.Range(1, 11);
        float multiplier = 1f;
        string hitType = "";

        if (dice == 1)
        {
            Debug.Log($"⚔️ {attackerName} used {attack.attackName}! 🎲 Die: 1 ❌ Missed!");
            Debug.Log($"{targetName}'s HP: {targetRuntime.currentHP}/{targetRuntime.runtimeHP}");
            return;
        }
        else if (dice >= 8)
        {
            multiplier = 1.25f;
            hitType = "💥 Critical Hit!";
        }
        else if (dice <= 3)
        {
            multiplier = 0.75f;
            hitType = "🩹 Weak Hit!";
        }
        else
        {
            hitType = "⚔️ Normal Hit!";
        }

        int baseDamage = Mathf.Max(1, attack.power + attackerRuntime.Attack - targetRuntime.Defense / 2);
        int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

        targetRuntime.TakeDamage(finalDamage);

        Debug.Log($"⚔️ {attackerName} used {attack.attackName}! 🎲 {dice} → {hitType}");
        Debug.Log($"💥 {targetName} took {finalDamage} damage (HP: {targetRuntime.currentHP}/{targetRuntime.runtimeHP})");

        StartCoroutine(HitShake(target.transform));

        if (!targetRuntime.IsAlive)
        {
            Debug.Log($"💀 {targetName} fainted!");
            if (attacker.isPlayer && expSystem != null)
                expSystem.GrantXP(target.characterData);

            StartCoroutine(FadeAndRemove(target));

            if (isRecruitmentBattle && recruitTarget == target)
{
    Debug.Log($"❌ Recruit {recruitTarget.characterData.characterName} was defeated and will not return.");
    if (FindObjectOfType<RecruitmentManager>() != null)
        FindObjectOfType<RecruitmentManager>().ResetRecruitment();
    recruitTarget = null;
    isRecruitmentBattle = false;
    recruitmentComplete = true;
}

            
        }
    }

    // ==========================================================
    // Helpers (Fade, Shake etc.)
    // ==========================================================
   private IEnumerator FadeAndRemove(CharacterBattleController target)
{
    if (target == null) yield break;

    var sr = target.GetComponent<SpriteRenderer>();
    var selector = target.GetComponent<EnemySelector>();
    if (selector != null) selector.Highlight(false);

    if (sr != null)
    {
        Color c = sr.color;
        for (float t = 0; t < 1f; t += Time.deltaTime)
        {
            // check each frame that the renderer still exists
            if (sr == null || target == null)
                yield break;

            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
            yield return null;
        }
    }

    // Safety check before destroying
    if (target != null)
        Destroy(target.gameObject);
}


    private IEnumerator HitShake(Transform target)
    {
        if (target == null) yield break;
        Vector3 originalPos = target.position;
        float shakeDuration = 0.2f;
        float shakeStrength = 0.1f;

        for (float t = 0; t < shakeDuration; t += Time.deltaTime)
        {
            target.position = originalPos + (Vector3)Random.insideUnitCircle * shakeStrength;
            yield return null;
        }

        target.position = originalPos;
    }

    private bool AreAllDead(List<CharacterBattleController> list)
    {
        foreach (var c in list)
            if (c != null && c.GetRuntimeCharacter().IsAlive)
                return false;
        return true;
    }

    private IEnumerator HandleVictory(List<CharacterBattleController> defeatedEnemies)
    {
        Debug.Log("🏆 Victory! All enemies defeated!");
        battleActive = false;

        foreach (var enemy in defeatedEnemies)
        {
            if (enemy == null) continue;
            var selector = enemy.GetComponent<EnemySelector>();
            if (selector != null) selector.DisableSelection();
            StartCoroutine(FadeAndRemove(enemy));
        }

        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        yield return new WaitForSeconds(1.2f);
        Debug.Log("🎉 Battle complete! XP distributed successfully!");
        Debug.Log("--------------------------------------------------------");

        if (isRecruitmentBattle && !recruitmentComplete)
        {
            Debug.Log("⚠️ Recruitment battle ended (no recruit). Marking recruitment complete.");
            recruitmentComplete = true;
            isRecruitmentBattle = false;
            if (uiManager != null) uiManager.SetPersuadeButtonActive(false);
        }

        if (encounterManager != null)
            encounterManager.EndEncounter();
    }

    // ==========================================================
    // Persuasion (recruit) processing
    // ==========================================================
    public void TryPersuade(CharacterBattleController explicitTarget)
    {
        if (explicitTarget == null)
        {
            Debug.LogWarning("⚠️ TryPersuade called with null target.");
            return;
        }

        if (!isRecruitmentBattle)
        {
            Debug.Log("❌ Not a recruitment battle.");
            return;
        }

        recruitTarget = explicitTarget;
        TryPersuade();
    }

    public void TryPersuade()
    {
        if (!isRecruitmentBattle || recruitTarget == null)
        {
            Debug.Log("❌ No recruitment target!");
            return;
        }

        if (persuadeAttempts >= maxPersuadeAttempts)
        {
            Debug.Log("😤 You've used all your persuasion attempts!");
            StartCoroutine(FinishRecruitment(false));
            return;
        }

        persuadeAttempts++;

        var targetRuntime = recruitTarget.GetRuntimeCharacter();
        float hpRatio = (float)targetRuntime.currentHP / targetRuntime.runtimeHP; // 0..1

        float persuasionChance;
        if (hpRatio <= 0.02f) persuasionChance = 0.99f;
        else if (hpRatio <= 0.10f) persuasionChance = 0.85f;
        else if (hpRatio <= 0.20f) persuasionChance = 0.65f;
        else if (hpRatio <= 0.30f) persuasionChance = 0.45f;
        else if (hpRatio <= 0.50f) persuasionChance = 0.30f;
        else if (hpRatio <= 0.70f) persuasionChance = 0.20f;
        else if (hpRatio <= 0.80f) persuasionChance = 0.15f;
        else if (hpRatio <= 0.90f) persuasionChance = 0.10f;
        else if (hpRatio <= 0.99f) persuasionChance = 0.07f;
        else persuasionChance = 0.05f;

        Debug.Log($"🎯 Persuasion attempt {persuadeAttempts}/{maxPersuadeAttempts} — HP {hpRatio * 100f:0.0}% → chance {(persuasionChance * 100f):0.0}%");

        if (Random.value < persuasionChance)
        {
            Debug.Log("💖 Recruitment successful!");
            StartCoroutine(HandleRecruitmentSuccess());
        }
        else
        {
            Debug.Log("💬 Recruitment failed this attempt.");
            if (persuadeAttempts >= maxPersuadeAttempts)
            {
                Debug.Log("😔 No attempts left — recruit lost interest.");
                StartCoroutine(FinishRecruitment(false));
            }
        }
    }

    // ==========================================================
    // Recruit success/failure flows
    // ==========================================================
    private IEnumerator HandleRecruitmentSuccess()
    {
        yield return new WaitForSeconds(0.6f);

        if (recruitTarget == null)
        {
            Debug.LogError("❌ recruitTarget null on success.");
            yield break;
        }

        var recruitRuntime = recruitTarget.GetRuntimeCharacter();
        if (recruitRuntime == null)
        {
            Debug.LogError("❌ recruit runtime null on success.");
            yield break;
        }

        // Fade & remove recruit from battlefield so they can "join"
        yield return StartCoroutine(FadeAndRemove(recruitTarget));

        var playerRuntimes = PersistentPlayerData.Instance.GetAllPlayerRuntimes();

        if (playerRuntimes.Count < 3)
        {
            PersistentPlayerData.Instance.UpdateFromRuntime(recruitRuntime);
            Debug.Log($"🎉 {recruitRuntime.baseData.characterName} joined your team!");
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
            yield return StartCoroutine(FinishRecruitment(true));
            yield break;
        }
        else
        {
            Debug.Log("⚠️ Team full — press 1, 2 or 3 to replace a member.");
            bool replaced = false;
            while (!replaced)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    replaced = ReplaceMemberByIndex(0, recruitRuntime);
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                    replaced = ReplaceMemberByIndex(1, recruitRuntime);
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                    replaced = ReplaceMemberByIndex(2, recruitRuntime);

                yield return null;
            }
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
            yield return StartCoroutine(FinishRecruitment(true));
            yield break;
        }
    }

    private bool ReplaceMemberByIndex(int index, CharacterRuntime newRuntime)
    {
        var runtimes = PersistentPlayerData.Instance.GetAllPlayerRuntimes();
        if (index < 0 || index >= runtimes.Count) return false;

        var old = runtimes[index];
        if (old == null) return false;

        Debug.Log($"🔁 Replacing {old.baseData.characterName} with {newRuntime.baseData.characterName}.");
        PersistentPlayerData.Instance.UpdateFromRuntime(newRuntime);
        PersistentPlayerData.Instance.RemoveCharacter(old.baseData.characterName);
        return true;
    }

    private IEnumerator FinishRecruitment(bool success)
    {
        isRecruitmentBattle = false;
        recruitmentComplete = true;

        if (uiManager != null) uiManager.SetPersuadeButtonActive(false);
        if (PersistentPlayerData.Instance != null) PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        // If failed, fade & remove recruit (runs away)
        if (!success && recruitTarget != null)
        {
            Debug.Log($"💨 {recruitTarget.characterData.characterName} ran away after failed persuasion!");
            yield return StartCoroutine(FadeAndRemove(recruitTarget));
        }

        yield return new WaitForSeconds(0.3f);

        // If all enemies gone, end encounter
        if (AreAllDead(enemyControllers))
        {
            yield return StartCoroutine(HandleVictory(enemyControllers));
        }
        else
        {
            // Remove recruit target from enemyControllers if still present
            enemyControllers.Remove(recruitTarget);
        }
    }

    /// <summary>
/// Cleans up any leftover character prefabs or clones from the previous battle.
/// </summary>
private void CleanupOldInstances()
{
    Debug.Log("🧹 Cleaning up old BattleManager instances and spawned characters...");

    // 1️⃣ Clean characters left under spawn points
    ClearSpawnedCharacters();

    // 2️⃣ Destroy all CharacterBattleControllers in scene that aren’t part of this new battle
    var oldControllers = FindObjectsOfType<CharacterBattleController>();
    foreach (var c in oldControllers)
    {
        // skip ones that are already destroyed or null
        if (c == null) continue;

        // double-check if it's a leftover (not parented to any spawn point)
        bool isUnderPlayerSpawn = false;
        bool isUnderEnemySpawn = false;

        if (playerSpawnPoints != null)
        {
            foreach (Transform p in playerSpawnPoints)
            {
                if (c.transform.IsChildOf(p))
                {
                    isUnderPlayerSpawn = true;
                    break;
                }
            }
        }

        if (enemySpawnPoints != null)
        {
            foreach (Transform e in enemySpawnPoints)
            {
                if (c.transform.IsChildOf(e))
                {
                    isUnderEnemySpawn = true;
                    break;
                }
            }
        }

        // If not attached to any current spawn point, remove it
        if (!isUnderPlayerSpawn && !isUnderEnemySpawn)
        {
            Debug.Log($"🗑️ Destroying leftover character prefab: {c.name}");
            DestroyImmediate(c.gameObject);
        }
    }

    // 3️⃣ Clear local controller lists
    playerControllers.Clear();
    enemyControllers.Clear();

    // 4️⃣ Also make sure we don't have any pending coroutines running from old battles
    StopAllCoroutines();

    Debug.Log("✅ Cleanup complete. Scene ready for new battle.");
}


  

    // Background fading helper
    private IEnumerator FadeRegionBackground(RegionData newRegion)
    {
        if (backgroundImageUI == null)
        {
            Debug.LogWarning("⚠️ No backgroundImageUI assigned in BattleManager!");
            yield break;
        }

        float duration = 1f;
        float t = 0f;
        Color startColor = backgroundImageUI.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0);

        while (t < duration)
        {
            t += Time.deltaTime;
            backgroundImageUI.color = Color.Lerp(startColor, endColor, t / duration);
            yield return null;
        }

        if (newRegion != null && newRegion.backgroundImage != null)
        {
            backgroundImageUI.sprite = newRegion.backgroundImage;
            backgroundImageUI.preserveAspect = true;
        }

        t = 0f;
        startColor = backgroundImageUI.color;
        endColor = new Color(startColor.r, startColor.g, startColor.b, 1);

        while (t < duration)
        {
            t += Time.deltaTime;
            backgroundImageUI.color = Color.Lerp(startColor, endColor, t / duration);
            yield return null;
        }

        backgroundImageUI.color = endColor;
    }
    
}
