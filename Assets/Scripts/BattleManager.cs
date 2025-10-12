using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using Random = UnityEngine.Random;
using TMPro;


/// <summary>
/// Central battle manager. Handles spawning, turns, messages (queued), recruitment and integration with ExperienceSystem / UI.
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("Battle Messages")]
    public BattleMessageUI messageUI; // assign in inspector

    // Coroutine handle for the running ProcessMessageQueue() so we can stop it.
private Coroutine messageQueueCoroutine = null;


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

    [Header("Healthbar UI")]
    public GameObject healthBarPrefab;               // assign your HealthBar prefab
    public Transform playerHealthContainer;         // UI container for player bars
    public Transform enemyHealthContainer;          // UI container for enemy bars

    // runtime mapping from spawned character controllers -> their healthbar controllers
    private Dictionary<CharacterBattleController, HealthbarController> healthbarMap =
        new Dictionary<CharacterBattleController, HealthbarController>();

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

    // ---------- Message queue state ----------
    private class MessageRequest
    {
        public string text;
        public bool completed;
    }
    private Queue<MessageRequest> messageQueue = new Queue<MessageRequest>();
    private bool processingMessageQueue = false;
    private Coroutine activeMessageCoroutine = null;


// Currently-processing message (so Cancel/Hides can mark it completed).
private MessageRequest currentMessageRequest = null;

    // -------------------------
    // Unity lifecycle
    // -------------------------
    void Start()
    {
        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();
        encounterManager = FindFirstObjectByType<EncounterManager>();

        if (uiManager == null) Debug.LogWarning("⚠️ No BattleUIManager found in scene (BattleManager.Start).");
        if (expSystem == null) Debug.LogWarning("⚠️ No ExperienceSystem found in scene (BattleManager.Start).");

        // If inspector teams are present, start automatically (helpful for testing)
        if (playerTeam != null && playerTeam.Count > 0 && enemyTeam != null && enemyTeam.Count > 0)
        {
            StartBattle(playerTeam, enemyTeam, currentRegion);
        }
    }

    // ==========================================================
    // Public API to start battles
    // ==========================================================
    public void StartBattle(List<CharacterData> playerTeamData, List<CharacterData> enemyTeamData, RegionData region = null)
    {
        Debug.Log("⚔️ Starting new battle...");

        // Cleanup previous instances first
        CleanupOldInstances();

        // Reset internal state
        StopAllCoroutines();
        battleActive = false;
        chosenActions.Clear();
        playerControllers.Clear();
        enemyControllers.Clear();
        healthbarMap.Clear();

        // Validate input lists
        playerTeam = playerTeamData ?? new List<CharacterData>();
        enemyTeam = enemyTeamData ?? new List<CharacterData>();
        currentRegion = region;

        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();
        encounterManager = FindFirstObjectByType<EncounterManager>();

        if (uiManager == null) Debug.LogError("❌ No BattleUIManager found!");
        if (expSystem == null) Debug.LogError("❌ No ExperienceSystem found!");
        if (encounterManager == null) Debug.LogWarning("⚠️ No EncounterManager found (if you expect one).");

        if (characterPrefab == null)
        {
            Debug.LogError("❌ characterPrefab is not assigned on BattleManager — cannot spawn characters.");
            return;
        }

        if ((playerSpawnPoints == null || playerSpawnPoints.Length == 0) && playerTeam.Count > 0)
            Debug.LogWarning("⚠️ playerSpawnPoints is null or empty — players may not spawn correctly.");
        if ((enemySpawnPoints == null || enemySpawnPoints.Length == 0) && enemyTeam.Count > 0)
            Debug.LogWarning("⚠️ enemySpawnPoints is null or empty — enemies may not spawn correctly.");

        // Fade background if we have region data
        if (currentRegion != null && backgroundImageUI != null)
        {
            Debug.Log($"🌄 Starting battle in region: {currentRegion.regionName}");
            StartCoroutine(FadeRegionBackground(currentRegion));
        }

        // Clean old spawned objects then spawn teams
        ClearSpawnedCharacters();
        SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
        SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

        // Debug info about what actually got spawned
        Debug.Log($"✅ StartBattle: spawned players {playerControllers.Count}, enemies {enemyControllers.Count}");
        for (int i = 0; i < enemyControllers.Count; i++)
        {
            var e = enemyControllers[i];
            Debug.Log($"[Enemy {i}] {(e != null && e.characterData != null ? e.characterData.characterName : "NULL")}");
        }

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

        // Initialize EXP using the actual spawned players list
        if (expSystem != null) expSystem.Initialize(playerControllers, this);

        // Only start loop if we have both sides present
        if (playerControllers.Count == 0)
        {
            Debug.LogWarning("⚠️ No player controllers spawned — aborting battle start.");
            return;
        }
        if (enemyControllers.Count == 0)
        {
            Debug.LogWarning("⚠️ No enemy controllers spawned — nothing to fight. Aborting battle start.");
            return;
        }

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

    // ==========================================================
    // Spawning / clearing helpers
    // ==========================================================
    private void ClearSpawnedCharacters()
    {
        // Destroy healthbars
        if (healthbarMap != null)
        {
            foreach (var kv in new Dictionary<CharacterBattleController, HealthbarController>(healthbarMap))
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            healthbarMap.Clear();
        }

        // Destroy children of spawn points safely
        if (playerSpawnPoints != null)
        {
            foreach (var t in playerSpawnPoints)
                if (t != null)
                    for (int i = t.childCount - 1; i >= 0; i--)
                        DestroyImmediateOrRuntime(t.GetChild(i).gameObject);
        }

        if (enemySpawnPoints != null)
        {
            foreach (var t in enemySpawnPoints)
                if (t != null)
                    for (int i = t.childCount - 1; i >= 0; i--)
                        DestroyImmediateOrRuntime(t.GetChild(i).gameObject);
        }

        playerControllers.Clear();
        enemyControllers.Clear();
    }

    private void DestroyImmediateOrRuntime(GameObject go)
    {
#if UNITY_EDITOR
        DestroyImmediate(go);
#else
        Destroy(go);
#endif
    }

    private void SpawnTeam(List<CharacterData> teamData, Transform[] spawnPoints, List<CharacterBattleController> list, bool isPlayer)
    {
        if (teamData == null || spawnPoints == null || characterPrefab == null)
        {
            Debug.LogWarning("SpawnTeam: invalid parameters (teamData/spawnPoints/characterPrefab).");
            return;
        }

        int spawnLimit = Mathf.Min(teamData.Count, spawnPoints.Length);
        if (teamData.Count > spawnPoints.Length)
            Debug.LogWarning($"SpawnTeam: only {spawnPoints.Length} spawn points but {teamData.Count} characters requested — spawning first {spawnLimit}.");

        for (int i = 0; i < spawnLimit; i++)
        {
            var spawnPos = spawnPoints[i];
            if (spawnPos == null)
            {
                Debug.LogWarning($"SpawnTeam: spawn point {i} is null, skipping.");
                continue;
            }

            var obj = Instantiate(characterPrefab, spawnPos.position + new Vector3(0, verticalOffset, 0), Quaternion.identity);
            if (obj == null)
            {
                Debug.LogError("SpawnTeam: Instantiate returned null.");
                continue;
            }

            obj.SetActive(true);

            var ctrl = obj.GetComponent<CharacterBattleController>();
            if (ctrl == null)
            {
                Debug.LogError("SpawnTeam: spawned prefab missing CharacterBattleController component.");
                Destroy(obj);
                continue;
            }

            ctrl.characterData = teamData[i];
            ctrl.isPlayer = isPlayer;

            // Initialize runtime and visuals
            try { ctrl.InitializeCharacter(); } catch (System.Exception ex) { Debug.LogWarning($"SpawnTeam: InitializeCharacter threw: {ex}"); }

            // Apply persistent player runtime prior to battle (players only)
            if (isPlayer && PersistentPlayerData.Instance != null)
            {
                try { PersistentPlayerData.Instance.ApplyToRuntime(ctrl.GetRuntimeCharacter()); } catch { }
            }

            // Flip enemy visuals
            if (!isPlayer)
            {
                var scale = obj.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * -1;
                obj.transform.localScale = scale;
            }

            list.Add(ctrl);

            // Create healthbar if prefab and container assigned
            if (healthBarPrefab != null)
            {
                Transform parent = isPlayer ? playerHealthContainer : enemyHealthContainer;
                if (parent != null)
                {
                    var hbObj = Instantiate(healthBarPrefab, parent);
var hb = hbObj.GetComponent<HealthbarController>();
if (hb != null)
{
    var runtime = ctrl.GetRuntimeCharacter();
    int lvl = runtime != null ? runtime.currentLevel : 1;
    string displayName = ctrl.characterData != null ? ctrl.characterData.characterName : ctrl.name;

    hb.Init(displayName, lvl, isPlayer);

    if (runtime != null)
    {
        float percent = runtime.runtimeHP > 0 ? (float)runtime.currentHP / runtime.runtimeHP : 1f;
        hb.SetHPInstant(percent);
    }

    // keep binding and mapping
    healthbarMap[ctrl] = hb;
    try { hb.BindRuntime(ctrl.GetRuntimeCharacter()); } catch { }

    // keep sibling index ordering
    try { hb.transform.SetSiblingIndex(i); } catch { }

    // APPLY EXACT HARD-CODED POSITIONS (this uses your provided numbers)
    try
    {
        var rt = hb.GetComponent<RectTransform>();
        ApplyHardcodedHealthbarPositions(rt, hbObj, isPlayer, i);
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning($"⚠️ ApplyHardcodedHealthbarPositions failed: {ex.Message}");
    }

    // XP init (unchanged)
    if (isPlayer && expSystem != null && runtime != null)
    {
        int storedXP = 0;
        try { storedXP = expSystem.GetStoredXPFor(runtime.baseData.characterName); } catch { storedXP = 0; }
        int xpToNext = 0;
        try { xpToNext = expSystem.GetXPToNextLevel(runtime.currentLevel); } catch { xpToNext = 0; }
        float xpPercent = xpToNext > 0 ? Mathf.Clamp01(storedXP / (float)xpToNext) : 0f;
        try { hb.AnimateXP(xpPercent); } catch { }
    }
}

                }
            }
        }
        Debug.Log($"SpawnTeam: spawned {list.Count} {(isPlayer ? "player" : "enemy")} controllers.");
    }
    // ==========================================================
    // Main loop & phases
    // ==========================================================
    private IEnumerator BattleLoop()
    {
        while (battleActive)
        {
            // Player turn / choose actions
            yield return StartCoroutine(PlayerCommandPhase());

            // Enemy decide
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

            // Assign callbacks to UI
            if (uiManager != null)
            {
                uiManager.onAttackConfirmed = (attack, target) =>
                {
                    chosenAttack = attack;
                    chosenTarget = target;
                    actionChosen = true;
                };

                uiManager.onPersuadeRequested = () =>
                {
                    CharacterBattleController target = null;
                    if (isRecruitmentBattle && recruitTarget != null && recruitTarget.GetRuntimeCharacter().IsAlive)
                        target = recruitTarget;
                    else
                    {
                        var enemiesAlive = enemyControllers.FindAll(e => e != null && e.GetRuntimeCharacter().IsAlive);
                        if (enemiesAlive.Count > 0) target = enemiesAlive[0];
                    }

                    if (target != null)
                    {
                        Debug.Log($"🗣️ {currentPlayer.characterData.characterName} attempting to persuade {target.characterData.characterName}...");
                        TryPersuade(target);
                    }
                    else Debug.LogWarning("⚠️ No valid persuasion target at this time.");

                    // Persuade ends the player's turn immediately
                    actionChosen = true;
                };
            }

            // Wait for player's selection
            yield return new WaitUntil(() => actionChosen);

            // If Attack chosen store it
            if (chosenAttack != null && chosenTarget != null)
            {
                chosenActions[currentPlayer] = (chosenAttack, chosenTarget);
            }

            // Clear UI callbacks and hide panels
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

    private void EnemyCommandPhase()
    {
        foreach (var enemy in enemyControllers)
        {
            var runtime = enemy.GetRuntimeCharacter();
            if (!runtime.IsAlive) continue;

            var attacks = runtime.equippedAttacks;
            if (attacks == null || attacks.Count == 0) continue;

            var attack = attacks[Random.Range(0, attacks.Count)];
            var targets = playerControllers.FindAll(p => p.GetRuntimeCharacter().IsAlive);
            if (targets.Count == 0) continue;

            var target = targets[Random.Range(0, targets.Count)];
            chosenActions[enemy] = (attack, target);
        }
    }

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

            yield return StartCoroutine(PerformAttackCoroutine(actor, target, attack));

            // WAIT while ExperienceSystem is processing level-up prompts
            if (expSystem != null)
            {
                yield return new WaitWhile(() => expSystem.IsProcessingLevelUps);
            }

            yield return new WaitForSeconds(1.5f);
        }
    }

    // ==========================================================
    // Attack logic / animation wrapper
    // ==========================================================
   public (int diceRoll, string hitType, int damageDealt) PerformAttack(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
{
    if (attacker == null || target == null || attack == null) return (0, "", 0);

    var runtime = attacker.GetRuntimeCharacter();

    // --- Skip turn if stunned ---
    if (attacker.ShouldSkipTurn())
    {
        Debug.Log($"💫 {attacker.characterData.characterName} is stunned and skips their turn!");
        return (0, "", 0);
    }

    bool consumed = false;
    if (attacker.isPlayer)
    {
        if (attack.currentUsage <= 0)
        {
            Debug.LogWarning($"⚠️ {attacker.characterData.characterName} tried to use {attack.attackName} but has no uses left!");
            return (0, "", 0);
        }
        attack.currentUsage--;
        consumed = true;
    }

    // We'll capture the first dice/hit/damage we encounter to show in the UI
    int firstDice = 0;
    string firstHitType = "";
    int firstDamage = 0;
    bool recordedFirst = false;

    // --- Hardcoded moves that do not roll dice ---
    if (attack.attackName == "Cosmic Corruptor")
    {
        var targetRuntime = target.GetRuntimeCharacter();
        targetRuntime.ApplyStatusEffect(AttackEffectType.Burn, 3);
        targetRuntime.ApplyStatusEffect(AttackEffectType.Poison, 3);
        int damageDealt = targetRuntime.TakeDamage(40, attacker.GetRuntimeCharacter());
        Debug.Log($"☄️ {attacker.characterData.characterName} used Cosmic Corruptor on {target.characterData.characterName}, dealing {damageDealt} damage and applying Burn & Poison for 3 turns!");
        return (0, "", damageDealt);
    }

    if (attack.attackName == "Ashina Stance")
    {
        attacker.EnterStance(2);
        Debug.Log($"🧘‍♂️ {attacker.characterData.characterName} uses Ashina Stance! No damage dealt, preparing for next move.");
        return (0, "", 0);
    }

    if (attack.attackName == "Assassinate")
    {
        var attackerRuntime = attacker.GetRuntimeCharacter();
        target.GetRuntimeCharacter().ApplyTemporaryMark(attackerRuntime, 2.5f, 1);
        Debug.Log($"🎯 {attacker.characterData.characterName} marked {target.characterData.characterName} for Assassination!");
        return (0, "", 0);
    }

    if (attack.attackName == "Purgatory")
    {
        var attackerRuntime = attacker.GetRuntimeCharacter();
        var targetRuntime = target.GetRuntimeCharacter();
        if (!targetRuntime.IsAlive)
        {
            Debug.LogWarning("⚠️ Invalid target for Purgatory!");
            return (0, "", 0);
        }
        float hpRatio = Mathf.Clamp01((float)attackerRuntime.currentHP / attackerRuntime.MaxHP);
        float damageMultiplier = 1f + (1f - hpRatio) * 2f; // up to 3x
        int baseDamage = Mathf.Max(1, attack.power + attackerRuntime.Attack - targetRuntime.Defense / 2);
        int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);
        int damageDealt = targetRuntime.TakeDamage(finalDamage, attackerRuntime);
        Debug.Log($"🔥 {attacker.characterData.characterName} unleashes PURGATORY! ({Mathf.RoundToInt(damageMultiplier * 100f)}% power) -> {damageDealt} dmg");
        if (attack.isLifeLeech)
        {
            int healAmount = Mathf.RoundToInt(damageDealt * attack.lifeLeechPercent);
            attackerRuntime.Heal(healAmount);
            Debug.Log($"🩸 {attacker.characterData.characterName} absorbed {healAmount} HP from Purgatory!");
        }
        return (0, "", damageDealt);
    }

    // Healing move (no dice)
    if (attack.healsTarget)
    {
        if (!target.GetRuntimeCharacter().IsAlive)
        {
            if (consumed && attacker.isPlayer)
            {
                attack.currentUsage++;
                consumed = false;
            }
            Debug.LogWarning("⚠️ Invalid target for healing!");
            return (0, "", 0);
        }

        int healAmount = Mathf.RoundToInt(attack.power + runtime.Attack * 0.5f);
        target.GetRuntimeCharacter().Heal(healAmount);
        Debug.Log($"💚 {attacker.characterData.characterName} healed {target.characterData.characterName} for {healAmount} HP using {attack.attackName}!");
        return (0, "", 0);
    }

    // Non-damage/setup/buffs (no dice)
    if (attack.isNonDamageMove)
    {
        if (attack.modifiesNextDice)
            runtime.SetNextDiceRange(attack.nextDiceMin, attack.nextDiceMax);
        if (attack.modifiesNextAttack)
            runtime.SetNextAttackMultiplier(attack.nextAttackMultiplier);

        List<CharacterBattleController> buffTargets = new List<CharacterBattleController>();
        if (attack.isAoE)
            buffTargets.AddRange(playerControllers.FindAll(p => p.GetRuntimeCharacter().IsAlive));
        else if (attack.manualBuffTargetSelection)
            buffTargets.Add(target);
        else if (attack.affectsSelf)
            buffTargets.Add(attacker);
        else
            buffTargets.Add(target);

        foreach (var buff in buffTargets)
        {
            var buffRuntime = buff.GetRuntimeCharacter();
            if (attack.buffAttack) buffRuntime.ModifyAttack(attack.buffAttackAmount);
            if (attack.buffDefense) buffRuntime.ModifyDefense(attack.buffDefenseAmount);
            if (attack.buffSpeed) buffRuntime.ModifySpeed(attack.buffSpeedAmount);

            if (attack.debuffAttack) buffRuntime.ModifyAttack(-attack.debuffAttackAmount);
            if (attack.debuffDefense) buffRuntime.ModifyDefense(-attack.debuffDefenseAmount);
            if (attack.debuffSpeed) buffRuntime.ModifySpeed(-attack.debuffSpeedAmount);

            if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
                buffRuntime.ApplyStatusEffect(attack.effectType, attack.effectDuration);
        }

        Debug.Log($"✨ {attacker.characterData.characterName} used {attack.attackName} on {buffTargets.Count} target(s)!");
        return (0, "", 0);
    }

    // --- DAMAGE moves (one or many targets) ---
    List<CharacterBattleController> targets = new List<CharacterBattleController>();
if (attack.isAoE)
{
    var opponents = attacker.isPlayer ? enemyControllers : playerControllers;
    targets.AddRange(opponents.FindAll(o => o != null && o.GetRuntimeCharacter().IsAlive));
}
else
{
    targets.Add(target);
}

    bool appliedSelfEffects = false;
    foreach (var tgt in targets)
    {
        if (!tgt.GetRuntimeCharacter().IsAlive) continue;

        int diceRoll = (runtime.nextDiceMin > 0 && runtime.nextDiceMax > 0)
            ? Random.Range(runtime.nextDiceMin, runtime.nextDiceMax + 1)
            : Random.Range(attack.diceMin, attack.diceMax + 1);

        int usedPower = attack.power;
        if (attack.attackName == "Showdown")
        {
            usedPower = (Random.value <= 0.5f) ? 100 : 20;
            Debug.Log($"🎲 {attacker.characterData.characterName} uses Showdown! Power rolled: {usedPower}");
        }

        // 🎯 NEW SECTION — Handle Miss
        if (diceRoll == 1)
        {
            Debug.Log($"❌ {attacker.characterData.characterName}'s attack MISSED {tgt.characterData.characterName}!");
            if (!recordedFirst)
            {
                firstDice = diceRoll;
                firstHitType = "Miss!";
                firstDamage = 0;
                recordedFirst = true;
            }
            // skip this target entirely — no damage or effects
            continue;
        }

        int baseDamage = Mathf.Max(1, usedPower + runtime.Attack - tgt.GetRuntimeCharacter().Defense / 2);

        if (runtime.nextDiceMin > 0 && runtime.nextDiceMax > 0)
            runtime.ResetNextDiceRange();

        float multiplier = diceRoll >= 8 ? 1.25f : diceRoll <= 3 ? 0.75f : 1f;

        // Stance special effects
        if (attacker.InStance)
        {
            switch (attack.attackName)
            {
                case "Ichimonji":
                    multiplier *= 2f;
                    Debug.Log($"🌀 {attacker.characterData.characterName} deals double damage with Ichimonji while in stance!");
                    break;
                case "Quick Slash":
                    if (Random.value <= attack.effectChance)
                    {
                        tgt.GetRuntimeCharacter().ApplyStatusEffect(AttackEffectType.Poison, attack.effectDuration);
                        Debug.Log($"☠️ {attacker.characterData.characterName}'s Quick Slash poisons {tgt.characterData.characterName} while in stance!");
                    }
                    break;
            }
        }

        if (runtime.nextAttackMultiplier != 1f)
            multiplier *= runtime.ConsumeAttackMultiplier();

        int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

        // Fallen-scaling etc.
        if (attack.scalesWithFallenEnemies)
        {
            int fallenEnemies = enemyControllers.FindAll(e => !e.GetRuntimeCharacter().IsAlive).Count;
            float tempMultiplier = 1f + fallenEnemies * attack.fallenEnemiesMultiplier;
            finalDamage = Mathf.RoundToInt(finalDamage * tempMultiplier);
        }

        if (attack.scalesWithFallenAllies)
        {
            int fallenAllies = attacker.isPlayer
                ? playerControllers.FindAll(p => !p.GetRuntimeCharacter().IsAlive).Count
                : enemyControllers.FindAll(e => !e.GetRuntimeCharacter().IsAlive).Count;
            float alliesMultiplier = 1f + fallenAllies * attack.fallenAlliesMultiplier;
            finalDamage = Mathf.RoundToInt(finalDamage * alliesMultiplier);
        }

        int damageDealt = tgt.GetRuntimeCharacter().TakeDamage(finalDamage, runtime);

        // record the first dice/hit/damage for UI
        if (!recordedFirst)
        {
            firstDice = diceRoll;
            firstHitType = multiplier > 1f ? "Strong Hit!" : multiplier < 1f ? "Weak Hit!" : "Normal Hit!";
            if (firstDice == 1)
                firstHitType = "Miss!";
            firstDamage = damageDealt;
            recordedFirst = true;
        }

        string hitTypeLog = multiplier > 1f ? "💥 Strong Hit!" : multiplier < 1f ? "🩹 Weak Hit!" : "⚔️ Normal Hit!";
        if (diceRoll == 1) hitTypeLog = "❌ Miss!";
        Debug.Log($"🎲 Dice Roll: {diceRoll} → {hitTypeLog}");
        Debug.Log($"⚔️ {attacker.characterData.characterName} dealt {damageDealt} damage to {tgt.characterData.characterName} using {attack.attackName}");

        if (attack.isLifeLeech)
        {
            int healAmount = Mathf.RoundToInt(damageDealt * attack.lifeLeechPercent);
            runtime.Heal(healAmount);
            Debug.Log($"🩸 {attacker.characterData.characterName} healed {healAmount} HP from Life Leech!");
        }

        // apply self/target buffs & status effects (kept identical to original logic)
        if (attack.applyEffectsToSelf && !attack.manualBuffTargetSelection)
        {
            if (!appliedSelfEffects)
            {
                if (attack.buffAttack) runtime.ModifyAttack(attack.buffAttackAmount);
                if (attack.buffDefense) runtime.ModifyDefense(attack.buffDefenseAmount);
                if (attack.buffSpeed) runtime.ModifySpeed(attack.buffSpeedAmount);

                if (attack.debuffAttack) runtime.ModifyAttack(-attack.debuffAttackAmount);
                if (attack.debuffDefense) runtime.ModifyDefense(-attack.debuffDefenseAmount);
                if (attack.debuffSpeed) runtime.ModifySpeed(-attack.debuffSpeedAmount);

                if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
                    runtime.ApplyStatusEffect(attack.effectType, attack.effectDuration);

                appliedSelfEffects = true;
            }
        }
        else if (!attack.manualBuffTargetSelection)
        {
            var buffTargetNormal = tgt.GetRuntimeCharacter();
            if (attack.buffAttack) buffTargetNormal.ModifyAttack(attack.buffAttackAmount);
            if (attack.buffDefense) buffTargetNormal.ModifyDefense(attack.buffDefenseAmount);
            if (attack.buffSpeed) buffTargetNormal.ModifySpeed(attack.buffSpeedAmount);

            if (attack.debuffAttack) buffTargetNormal.ModifyAttack(-attack.debuffAttackAmount);
            if (attack.debuffDefense) buffTargetNormal.ModifyDefense(-attack.debuffDefenseAmount);
            if (attack.debuffSpeed) buffTargetNormal.ModifySpeed(-attack.debuffSpeedAmount);

            if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
                buffTargetNormal.ApplyStatusEffect(attack.effectType, attack.effectDuration);
        }

        // XP will be handled by coroutine wrapper once animation completes
    }

    // return first dice/hit/damage (or zeros if none)
    return (firstDice, firstHitType, firstDamage);
}


    private IEnumerator PerformAttackCoroutine(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
    {
        if (attacker == null || target == null || attack == null) yield break;

        var attackerRuntime = attacker.GetRuntimeCharacter();
        var targetRuntime = target.GetRuntimeCharacter();

        // Apply attack logic and capture first dice/hit/damage for UI reveal
        var result = PerformAttack(attacker, target, attack);

        // Announce the attack using queued messages
        yield return StartCoroutine(ShowBattleMessage($"{attacker.characterData.characterName} used {attack.attackName}!"));

        if (result.diceRoll > 0)
        {
            yield return StartCoroutine(ShowBattleMessage($"{attacker.characterData.characterName} rolled a {result.diceRoll}! - {result.hitType}"));
        }

        // small hit shake (run in parallel)
        StartCoroutine(HitShake(target.transform));

        // Animate the target's healthbar if present
        if (healthbarMap.TryGetValue(target, out var hb))
        {
            float finalPercent = targetRuntime.runtimeHP > 0 ? (float)targetRuntime.currentHP / targetRuntime.runtimeHP : 0f;
            bool useXPFirst = target.isPlayer;
            yield return StartCoroutine(hb.AnimateDamageSequence(finalPercent, useXPFirst));
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
        }

        // Handle death and XP after animations
        if (!targetRuntime.IsAlive)
        {
            Debug.Log($"💀 {target.characterData.characterName} fainted!");
            yield return StartCoroutine(ShowBattleMessage($"{target.characterData.characterName} fainted!"));

            if (attacker.isPlayer && expSystem != null)
                expSystem.GrantXP(target.characterData);

            if (healthbarMap.ContainsKey(target))
            {
                var hbToRemove = healthbarMap[target];
                healthbarMap.Remove(target);
                if (hbToRemove != null) Destroy(hbToRemove.gameObject);
            }

            yield return StartCoroutine(FadeAndRemove(target));

            if (isRecruitmentBattle && recruitTarget == target)
            {
                Debug.Log($"❌ Recruit {recruitTarget.characterData.characterName} was defeated and will not return.");
                yield return StartCoroutine(ShowBattleMessage($"Recruit {recruitTarget.characterData.characterName} was defeated and will not return."));
                if (FindObjectOfType<RecruitmentManager>() != null)
                    FindObjectOfType<RecruitmentManager>().ResetRecruitment();
                recruitTarget = null;
                isRecruitmentBattle = false;
                recruitmentComplete = true;
            }
        }
    }

    // ==========================================================
    // Fade / shake / removal helpers
    // ==========================================================
    private IEnumerator FadeAndRemove(CharacterBattleController target)
    {
        if (target == null) yield break;

        var sr = target.GetComponent<SpriteRenderer>();

        // Try EnemySelector first, then TargetSelector
        var enemySelector = target.GetComponent<EnemySelector>();
        if (enemySelector != null)
        {
            enemySelector.Highlight(false);
        }
        else
        {
            var targetSelector = target.GetComponent<TargetSelector>();
            if (targetSelector != null) targetSelector.Highlight(false);
        }

        if (sr != null)
        {
            Color c = sr.color;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (sr == null || target == null) yield break;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
                yield return null;
            }
        }

        if (healthbarMap.TryGetValue(target, out var hbToDestroy))
        {
            healthbarMap.Remove(target);
            if (hbToDestroy != null) Destroy(hbToDestroy.gameObject);
        }

        if (target != null) Destroy(target.gameObject);
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

    // ==========================================================
    // Victory / post-battle flow
    // ==========================================================
    private IEnumerator HandleVictory(List<CharacterBattleController> defeatedEnemies)
    {
        Debug.Log("🏆 Victory! All enemies defeated!");
        yield return StartCoroutine(ShowBattleMessage("Victory! All enemies defeated!"));

        battleActive = false;

        foreach (var enemy in defeatedEnemies)
        {
            if (enemy == null) continue;

            var enemySel = enemy.GetComponent<EnemySelector>();
            if (enemySel != null) enemySel.DisableSelection();
            else
            {
                var targetSel = enemy.GetComponent<TargetSelector>();
                if (targetSel != null) targetSel.DisableSelection();
            }

            StartCoroutine(FadeAndRemove(enemy));
        }

        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        yield return new WaitForSeconds(1.2f);
        Debug.Log("🎉 Battle complete! XP distributed successfully!");
        Debug.Log("--------------------------------------------------------");

        // After battle, process any pending level-up move prompts sequentially
        if (expSystem != null)
        {
            yield return StartCoroutine(expSystem.ProcessPendingMovePrompts());
            if (PersistentPlayerData.Instance != null)
                PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
        }

        if (isRecruitmentBattle && !recruitmentComplete)
        {
            Debug.Log("⚠️ Recruitment battle ended (no recruit). Marking recruitment complete.");
            recruitmentComplete = true;
            isRecruitmentBattle = false;
            if (uiManager != null) uiManager.SetPersuadeButtonActive(false);
        }

        if (encounterManager != null) encounterManager.EndEncounter();
    }

    // ==========================================================
    // Persuasion / recruitment
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
        StartCoroutine(TryPersuade());
    }

    public IEnumerator TryPersuade()
    {
        if (!isRecruitmentBattle || recruitTarget == null)
        {
            Debug.Log("❌ No recruitment target!");
            yield break;
        }

        if (persuadeAttempts >= maxPersuadeAttempts)
        {
            Debug.Log("😤 You've used all your persuasion attempts!");
            yield return StartCoroutine(ShowBattleMessage("You've used all your persuasion attempts!"));
            StartCoroutine(FinishRecruitment(false));
            yield break;
        }

        persuadeAttempts++;

        var targetRuntime = recruitTarget.GetRuntimeCharacter();
        float hpRatio = targetRuntime.runtimeHP > 0 ? (float)targetRuntime.currentHP / targetRuntime.runtimeHP : 0f;

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
        yield return StartCoroutine(ShowBattleMessage($"Persuasion attempt {persuadeAttempts}/{maxPersuadeAttempts} — HP {hpRatio * 100f:0.0}% → chance {(persuasionChance * 100f):0.0}%"));

        if (Random.value < persuasionChance)
        {
            Debug.Log("💖 Recruitment successful!");
            yield return StartCoroutine(ShowBattleMessage("Recruitment successful!"));
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
            yield return StartCoroutine(ShowBattleMessage($"{recruitRuntime.baseData.characterName} joined your team!"));
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
            yield return StartCoroutine(FinishRecruitment(true));
            yield break;
        }
        else
        {
            Debug.Log("⚠️ Team full — press 1, 2 or 3 to replace a member.");
            yield return StartCoroutine(ShowBattleMessage($"Team full — press 1, 2 or 3 to replace a member."));
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
        if (old == null || newRuntime == null) return false;

        Debug.Log($"🔁 Replacing {old.baseData.characterName} with {newRuntime.baseData.characterName}...");

        PersistentPlayerData.Instance.ReplaceCharacter(old.baseData.characterName, newRuntime);
        PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        Debug.Log($"✅ Replacement complete: {old.baseData.characterName} → {newRuntime.baseData.characterName}");
        return true;
    }

    private IEnumerator FinishRecruitment(bool success)
    {
        isRecruitmentBattle = false;
        recruitmentComplete = true;

        if (uiManager != null) uiManager.SetPersuadeButtonActive(false);
        if (PersistentPlayerData.Instance != null) PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);

        if (!success && recruitTarget != null)
        {
            Debug.Log($"💨 {recruitTarget.characterData.characterName} ran away after failed persuasion!");
            yield return StartCoroutine(FadeAndRemove(recruitTarget));
        }

        // Clear message panel quickly
        if (messageUI != null) messageUI.HideInstant();

        yield return new WaitForSeconds(0.3f);

        if (AreAllDead(enemyControllers))
        {
            yield return StartCoroutine(HandleVictory(enemyControllers));
        }
        else
        {
            enemyControllers.Remove(recruitTarget);

            if (!AreAllDead(enemyControllers))
            {
                if (!battleActive)
                {
                    battleActive = true;
                    StartCoroutine(BattleLoop());
                }

                if (uiManager != null)
                {
                    uiManager.SetPersuadeButtonActive(isRecruitmentBattle);
                    if (playerChoiceIndex >= 0 && playerChoiceIndex < playerControllers.Count)
                    {
                        uiManager.playerController = playerControllers[playerChoiceIndex];
                        uiManager.ShowMainActions();
                    }
                }
            }
        }
    }

    // ==========================================================
    // Cleanup prior to starting (destroy leftovers)
    // ==========================================================
    private void CleanupOldInstances()
    {
        Debug.Log("🧹 Cleaning up old BattleManager instances and spawned characters...");

        ClearSpawnedCharacters();

        var oldControllers = FindObjectsOfType<CharacterBattleController>();
        foreach (var c in oldControllers)
        {
            if (c == null) continue;

            bool isUnderPlayerSpawn = false;
            bool isUnderEnemySpawn = false;

            if (playerSpawnPoints != null)
            {
                foreach (Transform p in playerSpawnPoints)
                {
                    if (p != null && c.transform.IsChildOf(p))
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
                    if (e != null && c.transform.IsChildOf(e))
                    {
                        isUnderEnemySpawn = true;
                        break;
                    }
                }
            }

            if (!isUnderPlayerSpawn && !isUnderEnemySpawn)
            {
                Debug.Log($"🗑️ Destroying leftover character prefab: {c.name}");
                DestroyImmediateOrRuntime(c.gameObject);
            }
        }

        playerControllers.Clear();
        enemyControllers.Clear();

        StopAllCoroutines();

        Debug.Log("✅ Cleanup complete. Scene ready for new battle.");
    }

    // ==========================================================
    // Background fade helper
    // ==========================================================
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

    // ==========================================================
    // Message queueing (safe, sequential messages)
    // ==========================================================
    /// <summary>
    /// Enqueue a message and optionally block until it has been displayed (typewriter + wait).
    /// Use blocking=true to wait until the message fully finishes.
    /// </summary>
    public IEnumerator ShowBattleMessage(string text, bool blocking = true)
{
    if (messageUI == null)
    {
        Debug.LogWarning("⚠️ messageUI not assigned!");
        yield break;
    }

    var req = new MessageRequest { text = text, completed = false };
    messageQueue.Enqueue(req);

    if (!processingMessageQueue)
    {
        // store the Coroutine handle so we can stop it later if needed
        messageQueueCoroutine = StartCoroutine(ProcessMessageQueue());
    }

    if (!blocking)
        yield break;

    // Blocking wait, but with a hard timeout fallback so we never hang forever.
    // We prefer to wait for req.completed, but if something goes wrong (race, external StopCoroutine),
    // we will force-complete after a short timeout.
   float maxWait = 12f;
float elapsed = 0f;
while (!req.completed && elapsed < maxWait)
{
    elapsed += Time.unscaledDeltaTime;
    yield return null;
}

    if (!req.completed)
    {
        // If we hit the timeout, warn and mark completed so the caller (battle loop etc.) continues.
        Debug.LogWarning($"⚠️ ShowBattleMessage('{text}') timed out after {maxWait}s — forcing continuation.");
        req.completed = true;
    }

    yield break;
}


private IEnumerator ProcessMessageQueue()
{
    if (processingMessageQueue) yield break;
    processingMessageQueue = true;

    while (messageQueue.Count > 0)
    {
        var req = messageQueue.Dequeue();
        if (req == null) continue;

        // expose to cancel function
        currentMessageRequest = req;

        // Ensure the message UI GameObject is active so its coroutines won't fail.
        if (messageUI != null && messageUI.gameObject != null && !messageUI.gameObject.activeInHierarchy)
            messageUI.gameObject.SetActive(true);

        if (activeMessageCoroutine != null)
        {
            try { StopCoroutine(activeMessageCoroutine); } catch { }
            activeMessageCoroutine = null;
        }

        bool typedDone = false;
        bool ttsDone = false;

        IEnumerator RunAndMark(IEnumerator job, System.Action markDone)
        {
            yield return StartCoroutine(job);
            try { markDone?.Invoke(); } catch { }
        }

        bool startedTyped = false;
        if (messageUI != null)
        {
            try
            {
                activeMessageCoroutine =
                    StartCoroutine(RunAndMark(messageUI.ShowMessage(req.text), () => typedDone = true));
                startedTyped = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"⚠️ Could not start typed message coroutine for '{req.text}': {ex.Message}");
                typedDone = true;
                activeMessageCoroutine = null;
            }
        }
        else
        {
            typedDone = true;
        }

        var tts = FindObjectOfType<MurfTTSStream>();
        if (tts != null)
        {
            try
            {
                string ttsContext = "battle";
                float ttsPadding = 0.12f;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"⚠️ Failed to start TTS for message '{req.text}': {ex.Message}");
                ttsDone = true;
            }
        }
        else
        {
            ttsDone = true;
        }

        if (!startedTyped && messageUI != null)
        {
            try
            {
                messageUI.ShowMessageInstant(req.text);
            }
            catch
            {
                Debug.Log(req.text);
            }
            yield return new WaitForSeconds(0.45f);
        }

        // Wait until both finished or a safety timeout OR until someone externally marks req.completed
       float safetyTimeout = 12f; // seconds (adjust if needed)
float elapsed = 0f;
while (!(typedDone && ttsDone) && elapsed < safetyTimeout && !req.completed)
{
    elapsed += Time.unscaledDeltaTime;
    yield return null;
}

        if (!(typedDone && ttsDone) && !req.completed)
        {
            Debug.LogWarning($"⚠️ Message '{req.text}' did not finish within {safetyTimeout}s; continuing.");
        }

        // Clear active typed reference
        activeMessageCoroutine = null;

        // Mark request completed (so any ShowBattleMessage callers waiting will continue).
        req.completed = true;

        // tiny buffer
        yield return null;

        // clear current request
        currentMessageRequest = null;
    }

    processingMessageQueue = false;
    // clear the stored coroutine handle
    messageQueueCoroutine = null;
}



    /// <summary>
    /// Cancel any currently queued or running messages and hide message UI instantly.
    /// </summary>
   public void CancelAndHideBattleMessage()
{
    // clear queued messages
    messageQueue.Clear();

    // If there's a currently-processing request, mark it completed so any blocking callers continue.
    if (currentMessageRequest != null)
    {
        try { currentMessageRequest.completed = true; } catch { }
        currentMessageRequest = null;
    }

    // Stop the main queue coroutine
    if (messageQueueCoroutine != null)
    {
        try { StopCoroutine(messageQueueCoroutine); } catch { }
        messageQueueCoroutine = null;
    }

    // Stop any typed-message coroutine we've stored
    if (activeMessageCoroutine != null)
    {
        try { StopCoroutine(activeMessageCoroutine); } catch { }
        activeMessageCoroutine = null;
    }

    // reset flag so ProcessMessageQueue won't be left in a weird state
    processingMessageQueue = false;

    // Hide message UI and aggressively clear any text so it doesn't linger
    if (messageUI != null)
    {
        try { messageUI.HideInstant(); } catch { }

        // best-effort: clear common text components inside messageUI so text doesn't linger on screen
        try
        {
            var textComp = messageUI.GetComponentInChildren<UnityEngine.UI.Text>();
            if (textComp != null) textComp.text = "";
        }
        catch { }

        try
        {
            // TextMeshPro support (if you use TMP)
            var tmp = messageUI.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = "";
        }
        catch { }
    }
}
private void ApplyHardcodedHealthbarPositions(RectTransform hbRect, GameObject hbObj, bool isPlayer, int spawnIndex)
{
    if (hbObj == null) return;

    // X coordinate common to healthbar
    float targetX = 8.17651f;

    // ==== Name text coordinates (user-provided) ====
    // Player name positions
    Vector2 playerNamePos0 = new Vector2(336.8f, 1004.8f);
    Vector2 playerNamePos1 = new Vector2(336.8f, 1002.3f);
    Vector2 playerNamePos2 = new Vector2(336.8f, 1005.6f);

    // Enemy name positions (typo corrected: second enemy x set to 336.8)
    Vector2 enemyNamePos0 = new Vector2(336.8f, 1004.4f);
    Vector2 enemyNamePos1 = new Vector2(336.8f, 1005.6f);

    // ==== XP & Healthbar placement ====
    if (isPlayer)
    {
        float targetY;
        float xpX = 465.5f;
        float xpY;

        switch (spawnIndex)
        {
            case 0:
                targetY = 179.8f;
                xpY = 955.2f;
                break;
            case 1:
                targetY = 84.99f;
                xpY = 956.4f;
                break;
            case 2:
                targetY = -0.50874f;
                xpY = 954.6f;
                break;
            default:
                targetY = 179.8f - spawnIndex * 94.81f;
                xpY = 955.2f;
                break;
        }

        // Healthbar position (anchored or local)
        if (hbRect != null)
        {
            hbRect.anchoredPosition = new Vector2(targetX, targetY);
        }
        else
        {
            try { hbObj.transform.localPosition = new Vector3(targetX, targetY, hbObj.transform.localPosition.z); } catch { }
        }

        // XP container child search & position (search for name containing "xpbar" or "xp")
        Transform xpChild = null;
        foreach (Transform c in hbObj.transform)
        {
            if (c == null || string.IsNullOrEmpty(c.name)) continue;
            string lower = c.name.ToLowerInvariant();
            if (lower.Contains("xpbar") || lower.Contains("xp") && lower.Contains("bar") || lower.Contains("xpcontainer"))
            {
                xpChild = c;
                break;
            }
        }

        if (xpChild != null)
        {
            var xpRect = xpChild as RectTransform;
            if (xpRect != null) xpRect.anchoredPosition = new Vector2(xpX, xpY);
            else
            {
                try { xpChild.localPosition = new Vector3(xpX, xpY, xpChild.localPosition.z); } catch { }
            }
        }

        // --- NameText placement ---
        Vector2 chosenNamePos = spawnIndex == 0 ? playerNamePos0 : spawnIndex == 1 ? playerNamePos1 : playerNamePos2;
        // find child with "name" in its name (case-insensitive)
        Transform nameChild = null;
        foreach (Transform c in hbObj.transform)
        {
            if (c == null || string.IsNullOrEmpty(c.name)) continue;
            if (c.name.ToLowerInvariant().Contains("name"))
            {
                nameChild = c;
                break;
            }
        }

        if (nameChild != null)
        {
            var nameRect = nameChild as RectTransform;
            if (nameRect != null) nameRect.anchoredPosition = chosenNamePos;
            else
            {
                try { nameChild.localPosition = new Vector3(chosenNamePos.x, chosenNamePos.y, nameChild.localPosition.z); } catch { }
            }
        }

        return;
    }

    // ===== Enemy placement (max 2) =====
    float enemyTargetY;
    switch (spawnIndex)
    {
        case 0: enemyTargetY = -0.50874f; break;
        case 1: enemyTargetY = -81.2f; break;
        default: enemyTargetY = -0.50874f - spawnIndex * 80f; break;
    }

    if (hbRect != null)
    {
        hbRect.anchoredPosition = new Vector2(targetX, enemyTargetY);
    }
    else
    {
        try { hbObj.transform.localPosition = new Vector3(targetX, enemyTargetY, hbObj.transform.localPosition.z); } catch { }
    }

    // Enemy name placement
    Vector2 chosenEnemyName = spawnIndex == 0 ? enemyNamePos0 : enemyNamePos1;
    Transform nameChildEnemy = null;
    foreach (Transform c in hbObj.transform)
    {
        if (c == null || string.IsNullOrEmpty(c.name)) continue;
        if (c.name.ToLowerInvariant().Contains("name"))
        {
            nameChildEnemy = c;
            break;
        }
    }

    if (nameChildEnemy != null)
    {
        var nameRect = nameChildEnemy as RectTransform;
        if (nameRect != null) nameRect.anchoredPosition = chosenEnemyName;
        else
        {
            try { nameChildEnemy.localPosition = new Vector3(chosenEnemyName.x, chosenEnemyName.y, nameChildEnemy.localPosition.z); } catch { }
        }
    }

    // (No XP reposition for enemy in provided list; add if you want)
}

}
