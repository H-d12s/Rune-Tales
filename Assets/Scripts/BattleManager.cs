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

    // === Hardcoded Ashina Stance ===
    if (attack.attackName == "Ashina Stance")
    {
        attacker.EnterStance(2);
        Debug.Log($"🧘‍♂️ {attacker.characterData.characterName} uses Ashina Stance! No damage dealt, preparing for next move.");
        return;
    }

    // === Healing Move ===
    if (attack.healsTarget)
    {
        if (!target.GetRuntimeCharacter().IsAlive)
        {
            Debug.LogWarning("⚠️ Invalid target for healing!");
            return;
        }

        int healAmount = Mathf.RoundToInt(attack.power + runtime.Attack * 0.5f);
        target.GetRuntimeCharacter().Heal(healAmount);
        Debug.Log($"💚 {attacker.characterData.characterName} healed {target.characterData.characterName} for {healAmount} HP using {attack.attackName}!");
        return;
    }

    // === Non-Damage / Setup Moves ===
    if (attack.isNonDamageMove)
    {
        if (attack.modifiesNextDice) runtime.SetNextDiceRange(attack.nextDiceMin, attack.nextDiceMax);
        if (attack.modifiesNextAttack) runtime.SetNextAttackMultiplier(attack.nextAttackMultiplier);

        var buffTarget = attack.affectsSelf ? runtime : target.GetRuntimeCharacter();

        if (attack.buffAttack) buffTarget.ModifyAttack(attack.buffAttackAmount);
        if (attack.buffDefense) buffTarget.ModifyDefense(attack.buffDefenseAmount);
        if (attack.buffSpeed) buffTarget.ModifySpeed(attack.buffSpeedAmount);

        if (attack.debuffAttack) buffTarget.ModifyAttack(-attack.debuffAttackAmount);
        if (attack.debuffDefense) buffTarget.ModifyDefense(-attack.debuffDefenseAmount);
        if (attack.debuffSpeed) buffTarget.ModifySpeed(-attack.debuffSpeedAmount);

        if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
            buffTarget.ApplyStatusEffect(attack.effectType, attack.effectDuration);

        Debug.Log($"🛠️ {attacker.characterData.characterName} used non-damage move {attack.attackName}");
        return;
    }

    // === Determine targets (AoE or single) ===
    List<CharacterBattleController> targets = new List<CharacterBattleController>();
    if (attack.isAoE)
    {
        if (attack.healsTarget)
            targets.AddRange(playerControllers.FindAll(p => p.GetRuntimeCharacter().IsAlive));
        else
            targets.AddRange(enemyControllers.FindAll(e => e.GetRuntimeCharacter().IsAlive));
    }
    else
    {
        targets.Add(target);
    }

    // --- Apply damage to targets ---
    foreach (var tgt in targets)
    {
        if (!tgt.GetRuntimeCharacter().IsAlive) continue;

        int diceRoll = (runtime.nextDiceMin > 0 && runtime.nextDiceMax > 0)
            ? Random.Range(runtime.nextDiceMin, runtime.nextDiceMax + 1)
            : Random.Range(attack.diceMin, attack.diceMax + 1);

        int baseDamage = Mathf.Max(1, attack.power + runtime.Attack - tgt.GetRuntimeCharacter().Defense / 2);

        runtime.nextDiceMin = runtime.nextDiceMax = 0;

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

        // === Fallen enemies scaling (Vengeance-like) ===
        if (attack.scalesWithFallenEnemies)
        {
            int fallenEnemies = enemyControllers.FindAll(e => !e.GetRuntimeCharacter().IsAlive).Count;
            float tempMultiplier = 1f + fallenEnemies * attack.fallenEnemiesMultiplier;
            finalDamage = Mathf.RoundToInt(finalDamage * tempMultiplier);
            Debug.Log($"🔥 {attacker.characterData.characterName}'s {attack.attackName} scaled with {fallenEnemies} fallen enemies! New damage: {finalDamage}");
        }

        int damageDealt = tgt.TakeDamage(finalDamage);

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

        // === Buffs / Debuffs for normal attacks (self-targeting handled) ===
        var buffTargetNormal = attack.affectsSelf ? runtime : tgt.GetRuntimeCharacter();

        if (attack.buffAttack) buffTargetNormal.ModifyAttack(attack.buffAttackAmount);
        if (attack.buffDefense) buffTargetNormal.ModifyDefense(attack.buffDefenseAmount);
        if (attack.buffSpeed) buffTargetNormal.ModifySpeed(attack.buffSpeedAmount);

        if (attack.debuffAttack) buffTargetNormal.ModifyAttack(-attack.debuffAttackAmount);
        if (attack.debuffDefense) buffTargetNormal.ModifyDefense(-attack.debuffDefenseAmount);
        if (attack.debuffSpeed) buffTargetNormal.ModifySpeed(-attack.debuffSpeedAmount);

        // === Status Effects ===
        if (attack.effectType != AttackEffectType.None && Random.value <= attack.effectChance)
            buffTargetNormal.ApplyStatusEffect(attack.effectType, attack.effectDuration);

        // === XP Gain ===
        if (!tgt.GetRuntimeCharacter().IsAlive && attacker.isPlayer && expSystem != null)
            expSystem.GrantXP(tgt.characterData);
    }
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




}
