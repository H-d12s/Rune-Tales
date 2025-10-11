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

    [Header("Healthbar UI")]
    public GameObject healthBarPrefab;               // assign your HealthBar prefab
    public Transform playerHealthContainer;         // UI container for player bars
    public Transform enemyHealthContainer;          // UI container for enemy bars

    // runtime mapping from spawned character controllers -> their healthbar controllers
    private Dictionary<CharacterBattleController, HealthbarController> healthbarMap =
        new Dictionary<CharacterBattleController, HealthbarController>();

    [Header("Teams (Editable)")]
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

    // -------------------------
    // Fallback Start: if you want to run this scene standalone (no EncounterManager)
    // -------------------------
    void Start()
    {
        // find core systems
        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();
        encounterManager = FindFirstObjectByType<EncounterManager>();

        if (uiManager == null) Debug.LogWarning("⚠️ No BattleUIManager found in scene (BattleManager.Start).");
        if (expSystem == null) Debug.LogWarning("⚠️ No ExperienceSystem found in scene (BattleManager.Start).");

        // If someone populated the playerTeam/enemyTeam in the inspector and wants the battle to auto-start
        if (playerTeam != null && playerTeam.Count > 0 && enemyTeam != null && enemyTeam.Count > 0)
        {
            StartBattle(playerTeam, enemyTeam, currentRegion);
        }
    }

    // ==========================================================
    // Called from EncounterManager (preferred entrypoint)
    // ==========================================================
    public void StartBattle(List<CharacterData> playerTeamData, List<CharacterData> enemyTeamData, RegionData region = null)
    {
        Debug.Log("⚔️ Starting new battle...");

        CleanupOldInstances();

        // Reset
        StopAllCoroutines();
        battleActive = false;
        chosenActions.Clear();
        playerControllers.Clear();
        enemyControllers.Clear();

        playerTeam = playerTeamData ?? new List<CharacterData>();
        enemyTeam = enemyTeamData ?? new List<CharacterData>();
        currentRegion = region;

        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();
        encounterManager = FindFirstObjectByType<EncounterManager>();

        if (uiManager == null) Debug.LogError("❌ No BattleUIManager found!");
        if (expSystem == null) Debug.LogError("❌ No ExperienceSystem found!");
        if (encounterManager == null) Debug.LogWarning("⚠️ No EncounterManager found (if you expect one).");

        // Fade background if we have region data
        if (currentRegion != null && backgroundImageUI != null)
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
        // Destroy any UI healthbars from previous battles
        if (healthbarMap != null)
        {
            foreach (var kv in healthbarMap)
            {
                var hb = kv.Value;
                if (hb != null)
                    Destroy(hb.gameObject);
            }
            healthbarMap.Clear();
        }

        // existing spawn point child destruction
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

            // Apply persistent runtime BEFORE battle starts (if available)
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

            // Healthbar integration (if prefab assigned)
            if (healthBarPrefab != null && (isPlayer ? playerHealthContainer : enemyHealthContainer) != null)
            {
                Transform parent = isPlayer ? playerHealthContainer : enemyHealthContainer;
                var hbObj = Instantiate(healthBarPrefab, parent);
                var hb = hbObj.GetComponent<HealthbarController>();
                if (hb != null)
                {
                    var runtime = ctrl.GetRuntimeCharacter();
                    int lvl = runtime != null ? runtime.currentLevel : 1;
                    string displayName = ctrl.characterData != null ? ctrl.characterData.characterName : ctrl.name;

                    hb.Init(displayName, lvl, isPlayer);

                    // initial hp fill
                    if (runtime != null)
                    {
                        float percent = runtime.runtimeHP > 0 ? (float)runtime.currentHP / runtime.runtimeHP : 1f;
                        hb.SetHPInstant(percent);
                    }

                    // set sibling order so bars match spawn order
                    hb.transform.SetSiblingIndex(i);

                    // store mapping
                    healthbarMap[ctrl] = hb;
                }
            }
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
    // Player decision phase — only active character can Persuade,
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
                uiManager.SetPlayerController(currentPlayer);
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
    // Resolve actions ordered by speed (uses coroutine performer)
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

            yield return StartCoroutine(PerformAttackCoroutine(actor, target, attack));

            // WAIT here if a level-up / move-replace prompt is running
            if (expSystem != null)
            {
                yield return new WaitWhile(() => expSystem.IsProcessingLevelUps);
            }

            yield return new WaitForSeconds(1.5f);
        }
    }

    // ==========================================================
    // Attack execution (move logic + many hardcoded mechanics)
    // Uses advanced move handling from the second file
    // ==========================================================
    public void PerformAttack(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
    {
        if (attacker == null || target == null || attack == null) return;

        var runtime = attacker.GetRuntimeCharacter();

        // --- Skip turn if stunned ---
        if (attacker.ShouldSkipTurn())
        {
            Debug.Log($"💫 {attacker.characterData.characterName} is stunned and skips their turn!");
            return;
        }

        // --- PP / Usage check (PLAYER ONLY) ---
        bool consumed = false;
        if (attacker.isPlayer)
        {
            if (attack.currentUsage <= 0)
            {
                Debug.LogWarning($"⚠️ {attacker.characterData.characterName} tried to use {attack.attackName} but has no uses left!");
                return;
            }

            // consume one use for player
            attack.currentUsage--;
            consumed = true;
        }

        // --- Hardcoded Cosmic Corruptor ---
        if (attack.attackName == "Cosmic Corruptor")
        {
            // Ensure target is valid
            if (target == null || !target.GetRuntimeCharacter().IsAlive)
            {
                Debug.LogWarning($"⚠️ Invalid target for Cosmic Corruptor!");
                return;
            }

            var targetRuntime = target.GetRuntimeCharacter();

            // Apply Burn and Poison
            targetRuntime.ApplyStatusEffect(AttackEffectType.Burn, 3);
            targetRuntime.ApplyStatusEffect(AttackEffectType.Poison, 3);

            // Deal fixed 40 damage
            int damageDealt = targetRuntime.TakeDamage(40, attacker.GetRuntimeCharacter());
            Debug.Log($"☄️ {attacker.characterData.characterName} used Cosmic Corruptor on {target.characterData.characterName}, dealing {damageDealt} damage and applying Burn & Poison for 3 turns!");

            return; // exit PerformAttack since this move is fully handled
        }

        // === Hardcoded Ashina Stance ===
        if (attack.attackName == "Ashina Stance")
        {
            attacker.EnterStance(2);
            Debug.Log($"🧘‍♂️ {attacker.characterData.characterName} uses Ashina Stance! No damage dealt, preparing for next move.");
            return;
        }

        if (attack.attackName == "Assassinate")
        {
            // Make sure the target is valid
            if (target == null || !target.GetRuntimeCharacter().IsAlive)
            {
                Debug.LogWarning($"⚠️ Invalid target for Assassinate!");
                return;
            }

            var targetRuntime = target.GetRuntimeCharacter();
            var attackerRuntime = attacker.GetRuntimeCharacter();

            // Apply the "mark" status
            targetRuntime.ApplyTemporaryMark(attackerRuntime, 2.5f, 1); // duration = 1 turn, multiplier = 2.5x

            Debug.Log($"🎯 {attacker.characterData.characterName} marked {target.characterData.characterName} for Assassination!");
            return;
        }

        if (attack.attackName == "Purgatory")
        {
            var attackerRuntime = attacker.GetRuntimeCharacter();
            var targetRuntime = target.GetRuntimeCharacter();

            if (!targetRuntime.IsAlive)
            {
                Debug.LogWarning("⚠️ Invalid target for Purgatory!");
                return;
            }

            // HP-based scaling: lower HP = more power
            float hpRatio = Mathf.Clamp01((float)attackerRuntime.currentHP / attackerRuntime.MaxHP);
            float damageMultiplier = 1f + (1f - hpRatio) * 2f; // up to 3x at 1 HP

            int baseDamage = Mathf.Max(1, attack.power + attackerRuntime.Attack - targetRuntime.Defense / 2);
            int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);

            int damageDealt = targetRuntime.TakeDamage(finalDamage, attackerRuntime);

            Debug.Log($"🔥 {attacker.characterData.characterName} unleashes PURGATORY! ({Mathf.RoundToInt(damageMultiplier * 100f)}% power)");
            Debug.Log($"💥 {attacker.characterData.characterName} dealt {damageDealt} damage to {target.characterData.characterName}!");

            // Optional: Life drain effect
            if (attack.isLifeLeech)
            {
                int healAmount = Mathf.RoundToInt(damageDealt * attack.lifeLeechPercent);
                attackerRuntime.Heal(healAmount);
                Debug.Log($"🩸 {attacker.characterData.characterName} absorbed {healAmount} HP from Purgatory!");
            }

            return;
        }

        // === Healing Move ===
        if (attack.healsTarget)
        {
            if (!target.GetRuntimeCharacter().IsAlive)
            {
                // Refund usage only if player consumed it
                if (consumed && attacker.isPlayer)
                {
                    attack.currentUsage++;
                    consumed = false;
                }

                Debug.LogWarning("⚠️ Invalid target for healing!");
                return;
            }

            int healAmount = Mathf.RoundToInt(attack.power + runtime.Attack * 0.5f);
            target.GetRuntimeCharacter().Heal(healAmount);
            Debug.Log($"💚 {attacker.characterData.characterName} healed {target.characterData.characterName} for {healAmount} HP using {attack.attackName}!");
            return;
        }

        // === Non-Damage / Setup Moves / Buffs ===
        if (attack.isNonDamageMove)
        {
            // Setup moves
            if (attack.modifiesNextDice)
                runtime.SetNextDiceRange(attack.nextDiceMin, attack.nextDiceMax);

            if (attack.modifiesNextAttack)
                runtime.SetNextAttackMultiplier(attack.nextAttackMultiplier);

            // Determine targets for buffs/debuffs
            List<CharacterBattleController> buffTargets = new List<CharacterBattleController>();

            if (attack.isAoE)
            {
                // AoE buffs/debuffs
                buffTargets.AddRange(playerControllers.FindAll(p => p.GetRuntimeCharacter().IsAlive));
            }
            else if (attack.manualBuffTargetSelection)
            {
                // Manual target selection: use the clicked target
                buffTargets.Add(target);
            }
            else if (attack.affectsSelf)
            {
                // Auto-buff self (UI auto-target behavior preserved)
                buffTargets.Add(attacker);
            }
            else
            {
                // Default: single target (like Fortify / Heal)
                buffTargets.Add(target);
            }

            // Apply buffs/debuffs
            foreach (var buff in buffTargets)
            {
                var buffRuntime = buff.GetRuntimeCharacter();

                if (attack.buffAttack) buffRuntime.ModifyAttack(attack.buffAttackAmount);
                if (attack.buffDefense) buffRuntime.ModifyDefense(attack.buffDefenseAmount);
                if (attack.buffSpeed) buffRuntime.ModifySpeed(attack.buffSpeedAmount);

                if (attack.debuffAttack) buffRuntime.ModifyAttack(-attack.debuffAttackAmount);
                if (attack.debuffDefense) buffRuntime.ModifyDefense(-attack.debuffDefenseAmount);
                if (attack.debuffSpeed) buffRuntime.ModifySpeed(-attack.debuffSpeedAmount);

                // Apply status effect if any
                if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
                    buffRuntime.ApplyStatusEffect(attack.effectType, attack.effectDuration);
            }

            Debug.Log($"✨ {attacker.characterData.characterName} used {attack.attackName} on {buffTargets.Count} target(s)!");
            return;
        }

        // === Determine targets (AoE or single) for damage moves ===
        List<CharacterBattleController> targets = new List<CharacterBattleController>();
        if (attack.isAoE)
        {
            targets.AddRange(enemyControllers.FindAll(e => e.GetRuntimeCharacter().IsAlive));
        }
        else
        {
            targets.Add(target);
        }

        // --- Apply damage to targets ---
        bool appliedSelfEffects = false; // ensure self effects only apply once (important for AoE)
        foreach (var tgt in targets)
        {
            if (!tgt.GetRuntimeCharacter().IsAlive) continue;

            int diceRoll = (runtime.nextDiceMin > 0 && runtime.nextDiceMax > 0)
                ? Random.Range(runtime.nextDiceMin, runtime.nextDiceMax + 1)
                : Random.Range(attack.diceMin, attack.diceMax + 1);

           int usedPower = attack.power;

        // If this move is "Showdown", roll its power for this use only (do NOT mutate AttackData)
        if (attack.attackName == "Showdown")
            {
            usedPower = (Random.value <= 0.5f) ? 100 : 20;
            Debug.Log($"🎲 {attacker.characterData.characterName} uses Showdown! Power rolled: {usedPower}");
            }

            int baseDamage = Mathf.Max(1, attack.power + runtime.Attack - tgt.GetRuntimeCharacter().Defense / 2);

            if (runtime.nextDiceMin > 0 && runtime.nextDiceMax > 0)
                runtime.ResetNextDiceRange();

            float multiplier = diceRoll >= 8 ? 1.25f : diceRoll <= 3 ? 0.75f : 1f;

            // === Hardcoded Stance Effects ===
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
            {
                multiplier *= runtime.ConsumeAttackMultiplier();
            }

            int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

            // === Fallen enemies scaling (Mortal-like) ===
            if (attack.scalesWithFallenEnemies)
            {
                int fallenEnemies = enemyControllers.FindAll(e => !e.GetRuntimeCharacter().IsAlive).Count;
                float tempMultiplier = 1f + fallenEnemies * attack.fallenEnemiesMultiplier;
                finalDamage = Mathf.RoundToInt(finalDamage * tempMultiplier);
                Debug.Log($"🔥 {attacker.characterData.characterName}'s {attack.attackName} scaled with {fallenEnemies} fallen enemies! New damage: {finalDamage}");
            }

            // === Fallen allies scaling (Vengeance-like) ===
            if (attack.scalesWithFallenAllies)
            {
                int fallenAllies = attacker.isPlayer
                    ? playerControllers.FindAll(p => !p.GetRuntimeCharacter().IsAlive).Count
                    : enemyControllers.FindAll(e => !e.GetRuntimeCharacter().IsAlive).Count;

                float alliesMultiplier = 1f + fallenAllies * attack.fallenAlliesMultiplier;
                finalDamage = Mathf.RoundToInt(finalDamage * alliesMultiplier);
                Debug.Log($"🔥 {attacker.characterData.characterName}'s {attack.attackName} scaled with {fallenAllies} fallen allies! New damage: {finalDamage}");
            }

            int damageDealt = tgt.GetRuntimeCharacter().TakeDamage(finalDamage, runtime);

            string hitType = multiplier > 1f ? "💥 Strong Hit!" : multiplier < 1f ? "🩹 Weak Hit!" : "⚔️ Normal Hit!";
            Debug.Log($"🎲 Dice Roll: {diceRoll} → {hitType}");
            Debug.Log($"⚔️ {attacker.characterData.characterName} dealt {damageDealt} damage to {tgt.characterData.characterName} using {attack.attackName}");

            // === Life Leech ===
            if (attack.isLifeLeech)
            {
                int healAmount = Mathf.RoundToInt(damageDealt * attack.lifeLeechPercent);
                runtime.Heal(healAmount);
                Debug.Log($"🩸 {attacker.characterData.characterName} healed {healAmount} HP from Life Leech!");
            }

            // === Optional buffs/debuffs applied to caster or self-targeting ===
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
                    Debug.Log($"🔁 {attacker.characterData.characterName} received self-effects from {attack.attackName}.");
                }
            }
            else if (attack.manualBuffTargetSelection)
            {
                // Manual buff selection assumed handled earlier (UI)
            }
            else
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

            // === XP Gain handled after animation in PerformAttackCoroutine when using that flow ===
        }

        // Note: consumed remains true for players (unless refunded earlier); no extra action needed here.
    }

    // Coroutine version used by ResolveActions so we can animate healthbars and fade characters
    private IEnumerator PerformAttackCoroutine(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
    {
        if (attacker == null || target == null || attack == null) yield break;

        var attackerRuntime = attacker.GetRuntimeCharacter();
        var targetRuntime = target.GetRuntimeCharacter();

        // Handle special one-off moves here first (so animations / hp display match the chosen behavior)
        // We'll use PerformAttack to apply logic to runtime, but since PerformAttack may return early
        // we need to replicate dice/hit logic for regular attacks to show messages consistently.
        // For simplicity, call PerformAttack to mutate runtimes / status effects / damage.
        // But we must still animate the healthbar and handle FadeAndRemove afterwards.

        // Run PerformAttack (which directly applies damage/status to runtimes)
        PerformAttack(attacker, target, attack);

        // After PerformAttack call, animate healthbar (if present)
        if (healthbarMap.TryGetValue(target, out var hb))
        {
            float finalPercent = targetRuntime.runtimeHP > 0 ? (float)targetRuntime.currentHP / targetRuntime.runtimeHP : 0f;
            bool useXPFirst = target.isPlayer; // players show xp-first, enemies skip
            yield return StartCoroutine(hb.AnimateDamageSequence(finalPercent, useXPFirst));
        }
        else
        {
            // small delay to preserve pacing
            yield return new WaitForSeconds(0.25f);
        }

        // small hit shake (run in parallel)
        StartCoroutine(HitShake(target.transform));

        // Handle death and XP
        if (!targetRuntime.IsAlive)
        {
            Debug.Log($"💀 {target.characterData.characterName} fainted!");
            if (attacker.isPlayer && expSystem != null)
                expSystem.GrantXP(target.characterData);

            // remove healthbar mapping if still present (FadeAndRemove will also try to do it)
            if (healthbarMap.ContainsKey(target))
            {
                var hbToRemove = healthbarMap[target];
                healthbarMap.Remove(target);
                if (hbToRemove != null) Destroy(hbToRemove.gameObject);
            }

            // start fade and remove of character sprite
            yield return StartCoroutine(FadeAndRemove(target));

            // recruitment special case as before (if applicable)
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
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                // check each frame that the renderer still exists
                if (sr == null || target == null)
                    yield break;

                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
                yield return null;
            }
        }

        if (healthbarMap.TryGetValue(target, out var hbToDestroy))
        {
            healthbarMap.Remove(target);
            if (hbToDestroy != null)
                Destroy(hbToDestroy.gameObject);
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

        // After battle, process any pending level-up move prompts (one after another).
        if (expSystem != null)
        {
            // This coroutine sets IsProcessingLevelUps while running.
            yield return StartCoroutine(expSystem.ProcessPendingMovePrompts());

            // After processing, persist any updates (moves/stats)
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
        float hpRatio = targetRuntime.runtimeHP > 0 ? (float)targetRuntime.currentHP / targetRuntime.runtimeHP : 0f; // 0..1

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
            if (c == null) continue;

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
