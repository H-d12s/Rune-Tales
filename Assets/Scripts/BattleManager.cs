using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    private List<CharacterBattleController> turnOrder = new List<CharacterBattleController>();
    private bool battleActive = false;


    private ExperienceSystem expSystem;
    private BattleUIManager uiManager;

    [Header("Spawn Points")]
    public Transform[] playerSpawnPoints;
    public Transform[] enemySpawnPoints;

    [Header("Prefabs")]
    public GameObject characterPrefab;

    [Header("Teams")]
    public List<CharacterData> playerTeam;
    public List<CharacterData> enemyTeam;

    private List<CharacterBattleController> playerControllers = new List<CharacterBattleController>();
    private List<CharacterBattleController> enemyControllers = new List<CharacterBattleController>();

    private int playerChoiceIndex = 0;

    private Dictionary<CharacterBattleController, (AttackData, CharacterBattleController)> chosenActions =
        new Dictionary<CharacterBattleController, (AttackData, CharacterBattleController)>();

    [Header("Spawn Offset")]
    public float verticalOffset = -1.5f;

    // ==========================
    // ⚙️ INITIALIZATION
    // ==========================
    void Start()
    {
        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();

        if (uiManager == null)
        {
            Debug.LogError("❌ No BattleUIManager found in scene!");
            return;
        }

        if (expSystem == null)
        {
            Debug.LogError("❌ No ExperienceSystem found in scene!");
            return;
        }

        // Spawn teams
        SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
        SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

        expSystem.Initialize(playerControllers);

        Debug.Log($"✅ Battle started: {playerControllers.Count} players vs {enemyControllers.Count} enemies");
        Debug.Log("--------------------------------------------------------");

        battleActive = true;
        StartCoroutine(BattleLoop());
    }

    private void SpawnTeam(List<CharacterData> teamData, Transform[] spawnPoints, List<CharacterBattleController> list, bool isPlayer)
    {
        for (int i = 0; i < teamData.Count && i < spawnPoints.Length; i++)
        {
            var obj = Instantiate(characterPrefab, spawnPoints[i].position + new Vector3(0, verticalOffset, 0), Quaternion.identity);
            var ctrl = obj.GetComponent<CharacterBattleController>();

            ctrl.characterData = teamData[i];
            ctrl.isPlayer = isPlayer;
            ctrl.InitializeCharacter();

            // Flip enemies visually
            if (!isPlayer)
            {
                var scale = obj.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * -1;
                obj.transform.localScale = scale;
            }

            list.Add(ctrl);
        }
    }

    // ==========================
    // 🔁 MAIN BATTLE LOOP
    // ==========================
    private IEnumerator BattleLoop()
    {
        while (battleActive)
        {
            // === Player Turn ===
            yield return StartCoroutine(PlayerCommandPhase());

            // === Enemy Turn ===
            EnemyCommandPhase();

            // === Resolve All Actions by Speed ===
            yield return StartCoroutine(ResolveActions());

            // === Tick End-of-Turn Effects for all characters ===
            foreach (var c in playerControllers)
                c.EndTurnEffects();
            foreach (var c in enemyControllers)
                c.EndTurnEffects();

            // === Check Victory/Defeat ===
            if (AreAllDead(enemyControllers))
            {
                StartCoroutine(HandleVictory(enemyControllers));
                yield break;
            }

            if (AreAllDead(playerControllers))
            {
                Debug.Log("💀 All players fainted! You lost...");
                battleActive = false;
                yield break;
            }

            chosenActions.Clear();
            playerChoiceIndex = 0;
        }
    }
    // ==========================
    // 🧠 PLAYER COMMAND PHASE
    // ==========================
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

            // Tell UI which player is choosing
            uiManager.SetPlayerController(currentPlayer);
            uiManager.BeginPlayerChoice((attack, target) =>
            {
                chosenAttack = attack;
                chosenTarget = target;
                actionChosen = true;
            });

            // Wait until the player picks an attack + target
            yield return new WaitUntil(() => actionChosen);

            // Determine default fallback target
            if (chosenAttack == null || chosenTarget == null)
            {
                Debug.LogWarning($"⚠️ No action chosen for {currentPlayer.characterData.characterName}, defaulting to first available attack.");
                var fallbackAttack = runtime.equippedAttacks.Count > 0 ? runtime.equippedAttacks[0] : null;

                if (fallbackAttack != null)
                {
                    // Choose target based on heal or damage
                    CharacterBattleController fallbackTarget = fallbackAttack.healsTarget
                        ? playerControllers.Find(p => p.GetRuntimeCharacter().IsAlive)
                        : enemyControllers.Find(e => e.GetRuntimeCharacter().IsAlive);

                    if (fallbackTarget != null)
                        chosenActions[currentPlayer] = (fallbackAttack, fallbackTarget);
                }
            }
            else
            {
                chosenActions[currentPlayer] = (chosenAttack, chosenTarget);
            }

            playerChoiceIndex++;
        }

        // Small delay to ensure all UI input is finalized before proceeding
        yield return new WaitForSeconds(0.2f);
    }

    // ==========================
    // 🤖 ENEMY AI PHASE
    // ==========================
    private void EnemyCommandPhase()
    {
        foreach (var enemy in enemyControllers)
        {
            var runtime = enemy.GetRuntimeCharacter();
            if (!runtime.IsAlive) continue;

            var attacks = runtime.equippedAttacks;
            if (attacks.Count == 0) continue;

            var attack = attacks[Random.Range(0, attacks.Count)];

            // Determine target list based on move type
            List<CharacterBattleController> possibleTargets;


            if (attack.healsTarget)
            {
                possibleTargets = enemyControllers.FindAll(e => e.GetRuntimeCharacter().IsAlive);
            }
            else
            {
                possibleTargets = playerControllers.FindAll(p => p.GetRuntimeCharacter().IsAlive);
            }

            if (possibleTargets.Count == 0) continue;

            var target = possibleTargets[Random.Range(0, possibleTargets.Count)];
            chosenActions[enemy] = (attack, target);
        }
    }

    // ==========================
    // ⚔️ ACTION RESOLUTION
    // ==========================
   private IEnumerator ResolveActions()
{
    // Build the turn order from whoever chose actions
    turnOrder = new List<CharacterBattleController>(chosenActions.Keys);

    // Sort with priority moves first, then by speed descending
    turnOrder.Sort((a, b) =>
    {
        // Safety: if one of the keys is missing in chosenActions (shouldn't happen),
        // treat the missing one as lower priority/speed.
        if (!chosenActions.ContainsKey(a) && !chosenActions.ContainsKey(b)) return 0;
        if (!chosenActions.ContainsKey(a)) return 1;
        if (!chosenActions.ContainsKey(b)) return -1;

        var attackA = chosenActions[a].Item1;
        var attackB = chosenActions[b].Item1;

        bool priA = attackA != null && attackA.usePriority;
        bool priB = attackB != null && attackB.usePriority;

        // Priority moves go first
        if (priA && !priB) return -1;
        if (!priA && priB) return 1;

        // If both same priority status, fallback to speed (descending)
        int speedA = a.GetRuntimeCharacter()?.Speed ?? 0;
        int speedB = b.GetRuntimeCharacter()?.Speed ?? 0;
        int speedCompare = speedB.CompareTo(speedA);
        if (speedCompare != 0) return speedCompare;

        // Final deterministic tie-breaker: prefer players over enemies (optional)
        // return (a.isPlayer && !b.isPlayer) ? -1 : (!a.isPlayer && b.isPlayer) ? 1 : 0;
        return 0;
    });

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


    // ==========================
    // 💥 ATTACK EXECUTION (Advanced version)
    // ==========================
public void PerformAttack(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
{
    if (attacker == null || target == null || attack == null)
    {
        Debug.LogError("❌ Invalid attack parameters!");
        return;
    }

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
        
        if (attack.attackName == "Showdown")
{
    attack.power = (Random.value <= 0.5f) ? 100 : 20;
    Debug.Log($"🎲 {attacker.characterData.characterName} uses Showdown! Power rolled: {attack.power}");
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
;

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
        // Use the new applyEffectsToSelf flag so UI auto-self-targeting (affectsSelf) remains unchanged.
        if (attack.applyEffectsToSelf && !attack.manualBuffTargetSelection)
        {
            if (!appliedSelfEffects)
            {
                // Apply buffs to the attacker runtime
                if (attack.buffAttack) runtime.ModifyAttack(attack.buffAttackAmount);
                if (attack.buffDefense) runtime.ModifyDefense(attack.buffDefenseAmount);
                if (attack.buffSpeed) runtime.ModifySpeed(attack.buffSpeedAmount);

                // Apply debuffs to the attacker runtime
                if (attack.debuffAttack) runtime.ModifyAttack(-attack.debuffAttackAmount);
                if (attack.debuffDefense) runtime.ModifyDefense(-attack.debuffDefenseAmount);
                if (attack.debuffSpeed) runtime.ModifySpeed(-attack.debuffSpeedAmount);

                // Apply status effect to self if configured
                if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
                    runtime.ApplyStatusEffect(attack.effectType, attack.effectDuration);

                appliedSelfEffects = true;
                Debug.Log($"🔁 {attacker.characterData.characterName} received self-effects from {attack.attackName}.");
            }
        }
        else if (attack.manualBuffTargetSelection)
        {
            // Manual buff selection assumed handled in the non-damage branch (do nothing here).
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

        // === XP Gain ===
        if (!tgt.GetRuntimeCharacter().IsAlive && attacker.isPlayer && expSystem != null)
            expSystem.GrantXP(tgt.characterData);
    }

    // Note: consumed remains true for players (unless refunded earlier); no extra action needed here.
}


    // ==========================
    // ✨ HELPERS
    // ==========================
    private IEnumerator FadeAndRemove(CharacterBattleController target)
    {
        var sr = target.GetComponent<SpriteRenderer>();
        var selector = target.GetComponent<TargetSelector>();
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
            var selector = enemy.GetComponent<TargetSelector>();
            if (selector != null) selector.DisableSelection();
            StartCoroutine(FadeAndRemove(enemy));
        }

        yield return new WaitForSeconds(1.2f);
        Debug.Log("🎉 Battle complete! XP distributed successfully!");
        Debug.Log("--------------------------------------------------------");
    }

    private IEnumerator PerformAssassinate(CharacterRuntime attacker, CharacterRuntime target, AttackData attack)
{
    if (target == null) yield break;

    Debug.Log($"🩸 {attacker.baseData.characterName} marks {target.baseData.characterName} for assassination!");
    target.markedBy = attacker;
    target.markDuration = 1; // lasts one turn only

    yield return new WaitForSeconds(0.5f);
}
}
