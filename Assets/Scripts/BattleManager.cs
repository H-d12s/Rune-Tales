using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;


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
    // 🚀 CALLED FROM ENCOUNTER MANAGER
    // ==========================================================
   public void StartBattle(List<CharacterData> playerTeamData, List<CharacterData> enemyTeamData, RegionData region = null)
{
    Debug.Log("⚔️ Starting new battle via EncounterManager...");

    // 🧹 Reset any previous battle coroutine and state
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

    // 🎨 Fade in region background if available
    if (currentRegion != null)
    {
        Debug.Log($"🌄 Starting battle in region: {currentRegion.regionName}");
        StartCoroutine(FadeRegionBackground(currentRegion));
    }

    // Clean old spawned objects
    ClearSpawnedCharacters();

    // === SPAWN TEAMS ===
    SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
    SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

    // === Recruitment battle setup ===
    if (isRecruitmentBattle)
    {
        recruitTarget = enemyControllers.Count > 0 ? enemyControllers[0] : null;
        persuadeAttempts = 0;
        recruitmentComplete = false;

        if (uiManager != null)
            uiManager.SetPersuadeButtonActive(true);

        Debug.Log("🎯 Recruitment battle started.");
    }
    else
    {
        if (uiManager != null)
            uiManager.SetPersuadeButtonActive(false);
    }

    // === Initialize EXP System AFTER persistence is applied ===
    expSystem.Initialize(playerControllers);

    // ✅ Begin battle loop
    battleActive = true;
    StartCoroutine(BattleLoop());

    Debug.Log($"✅ Battle started: {playerControllers.Count} players vs {enemyControllers.Count} enemies");
}


    /// <summary>
    /// Helper used by EncounterManager to start a recruitment battle where recruitData is the single enemy.
    /// </summary>
    public void StartRecruitmentBattle(List<CharacterData> playerTeamData, CharacterData recruitData)
    {
        if (recruitData == null)
        {
            Debug.LogError("❌ StartRecruitmentBattle called with null recruitData!");
            return;
        }

        isRecruitmentBattle = true;
        recruitmentComplete = false;
        // set enemyTeam to contain only the recruit
        var enemyList = new List<CharacterData> { recruitData };
        StartBattle(playerTeamData, enemyList);
    }

    // ----------------------------------------------------------
    // Small public helper for UI to list enemies / pick targets
    // ----------------------------------------------------------
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

            // ✅ Apply persistent runtime BEFORE battle starts
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
    // 🌀 MAIN BATTLE LOOP (unchanged)
    // ==========================================================
    private IEnumerator BattleLoop()
    {
        while (battleActive)
        {
            // === Player Turn ===
            yield return StartCoroutine(PlayerCommandPhase());

            // === Enemy Turn ===
            EnemyCommandPhase();

            // === Resolve All Actions ===
            yield return StartCoroutine(ResolveActions());

            // === Check Victory/Defeat ===
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
    // 🧠 PLAYER COMMAND PHASE (unchanged)
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

        uiManager.playerController = currentPlayer;
        uiManager.ShowMainActions();

        // 🎯 ATTACK chosen
        uiManager.onAttackConfirmed = (attack, target) =>
        {
            chosenAttack = attack;
            chosenTarget = target;
            actionChosen = true;
        };

        // 💬 PERSUADE chosen
        uiManager.onPersuadeChosen = () =>
        {
            // perform persuade logic immediately
            var enemies = enemyControllers.FindAll(e => e.GetRuntimeCharacter().IsAlive);
            if (enemies.Count > 0)
            {
                var target = enemies[0]; // always the first alive recruit
                TryPersuade(target);
            }

            Debug.Log($"🗣️ {currentPlayer.characterData.characterName} used Persuade and ends their turn.");
            actionChosen = true; // mark turn complete
        };

        // Wait for player input
        yield return new WaitUntil(() => actionChosen);

        // if Attack chosen, record it normally
        if (chosenAttack != null && chosenTarget != null)
        {
            chosenActions[currentPlayer] = (chosenAttack, chosenTarget);
        }

        playerChoiceIndex++;
    }

    yield return new WaitForSeconds(0.2f);
}



    // ==========================================================
    // 🤖 ENEMY COMMAND PHASE (unchanged)
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
    // ⚔️ ACTION RESOLUTION (unchanged)
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
    // 💥 ATTACK EXECUTION (unchanged)
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
        }
    }

    // ==========================================================
    // ✨ HELPERS (unchanged)
    // ==========================================================
    private IEnumerator FadeAndRemove(CharacterBattleController target)
    {
        var sr = target.GetComponent<SpriteRenderer>();
        var selector = target.GetComponent<EnemySelector>();
        if (selector != null)
            selector.Highlight(false);

        if (sr != null)
        {
            Color c = sr.color;
            for (float t = 0; t < 1f; t += Time.deltaTime)
            {
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
                yield return null;
            }
        }

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
            if (c.GetRuntimeCharacter().IsAlive)
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

        // ✅ Save player data BEFORE ending encounter
        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        yield return new WaitForSeconds(1.2f);
        Debug.Log("🎉 Battle complete! XP distributed successfully!");
        Debug.Log("--------------------------------------------------------");

        // If this was a recruitment battle but recruitment didn't complete (e.g. recruit died),
        // mark recruitmentComplete so EncounterManager won't hang.
        if (isRecruitmentBattle && !recruitmentComplete)
        {
            Debug.Log("⚠️ Recruitment battle ended (no recruit). Marking recruitment complete.");
            recruitmentComplete = true;
            isRecruitmentBattle = false;
            if (uiManager != null)
                uiManager.SetPersuadeButtonActive(false);
        }

        if (encounterManager != null)
            encounterManager.EndEncounter();
    }

    // ==========================================================
    // 💬 PERSUASION: called from UI (BattleUIManager) or code
    // ==========================================================
    // Public overload so UI can pass a specific target.
    public void TryPersuade(CharacterBattleController explicitTarget)
    {
        if (explicitTarget == null)
        {
            Debug.LogWarning("⚠️ TryPersuade called with null target.");
            return;
        }

        // ensure this is the recruit we are dealing with in recruitment mode
        if (!isRecruitmentBattle)
        {
            Debug.Log("❌ Not a recruitment battle.");
            return;
        }

        recruitTarget = explicitTarget;
        TryPersuade();
    }

    // Internal persuasion logic (uses recruitTarget)
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
            // Recruitment failed — mark complete and cleanup
            StartCoroutine(FinishRecruitment(false));
            return;
        }

        persuadeAttempts++;

        var targetRuntime = recruitTarget.GetRuntimeCharacter();
        float hpRatio = (float)targetRuntime.currentHP / targetRuntime.runtimeHP; // 0..1

        // Map hpRatio to chance bands (as requested)
      // 🎯 HP-based persuasion formula (no stacking chance per click)
float persuasionChance;

if (hpRatio <= 0.02f) persuasionChance = 0.99f;      // 1–2% HP
else if (hpRatio <= 0.10f) persuasionChance = 0.85f; // 2–10%
else if (hpRatio <= 0.20f) persuasionChance = 0.65f; // 10–20%
else if (hpRatio <= 0.30f) persuasionChance = 0.45f; // 20–30%
else if (hpRatio <= 0.50f) persuasionChance = 0.30f; // 30–50%
else if (hpRatio <= 0.70f) persuasionChance = 0.20f; // 50–70%
else if (hpRatio <= 0.80f) persuasionChance = 0.15f; // 70–80%
else if (hpRatio <= 0.90f) persuasionChance = 0.10f; // 80–90%
else if (hpRatio <= 0.99f) persuasionChance = 0.07f; // 90–99%
else persuasionChance = 0.05f;                       // 100% HP

// No attempt-based increase. Only HP matters.
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
    // Recruitment success/failure handling
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

    // 🎨 Fade out enemy (like dying animation)
    yield return StartCoroutine(FadeAndRemove(recruitTarget));

    // ✅ Add recruit to persistent data if space; otherwise prompt replacement
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
    }
}

    private bool ReplaceMemberByIndex(int index, CharacterRuntime newRuntime)
    {
        var runtimes = PersistentPlayerData.Instance.GetAllPlayerRuntimes();
        if (index < 0 || index >= runtimes.Count)
        {
            Debug.Log("❌ Invalid index for replacement.");
            return false;
        }

        var old = runtimes[index];
        if (old == null)
        {
            Debug.Log("❌ No member at that slot.");
            return false;
        }

        Debug.Log($"🔁 Replacing {old.baseData.characterName} with {newRuntime.baseData.characterName}.");

        // Add/update new runtime to persistent data
        PersistentPlayerData.Instance.UpdateFromRuntime(newRuntime);

        // Remove the old member (assumes RemoveCharacter exists in your Persistent system)
        PersistentPlayerData.Instance.RemoveCharacter(old.baseData.characterName);

        return true;
    }

   private IEnumerator FinishRecruitment(bool success)
{
    isRecruitmentBattle = false;
    recruitmentComplete = true;

    if (uiManager != null)
        uiManager.SetPersuadeButtonActive(false);

    if (PersistentPlayerData.Instance != null)
        PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

    // 🧹 If failed, fade & remove the recruit (enemy runs away)
    if (!success && recruitTarget != null)
    {
        Debug.Log($"💨 {recruitTarget.characterData.characterName} ran away after failed persuasion!");
        yield return StartCoroutine(FadeAndRemove(recruitTarget));
    }

    yield return new WaitForSeconds(0.3f);

    // 🧾 If all enemies gone, mark end of encounter
    if (AreAllDead(enemyControllers))
    {
        yield return StartCoroutine(HandleVictory(enemyControllers));
    }
    else
    {
        // Remove recruitTarget from list cleanly if it still exists
        enemyControllers.Remove(recruitTarget);
    }
}
private IEnumerator FadeRegionBackground(RegionData newRegion)
{
    if (backgroundImageUI == null)
    {
        Debug.LogWarning("⚠️ No backgroundImageUI assigned in BattleManager!");
        yield break;
    }

    // Fade out current background
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

    // Change background sprite
    if (newRegion != null && newRegion.backgroundImage != null)
    {
        backgroundImageUI.sprite = newRegion.backgroundImage;
        backgroundImageUI.preserveAspect = true;
    }

    // Fade in new background
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
