using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
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

    // ==========================================================
    // 🚀 CALLED FROM ENCOUNTER MANAGER
    // ==========================================================
    public void StartBattle(List<CharacterData> playerTeamData, List<CharacterData> enemyTeamData)
    {
        Debug.Log("⚔️ Starting new battle via EncounterManager...");
        Debug.Log("⚔️ Starting new battle via EncounterManager...");

    // 🧹 Reset any previous battle coroutine and state
        StopAllCoroutines();
        battleActive = false;
        chosenActions.Clear();
        playerControllers.Clear();
        enemyControllers.Clear();

        playerTeam = playerTeamData;
        enemyTeam = enemyTeamData;

        uiManager = FindFirstObjectByType<BattleUIManager>();
        expSystem = FindFirstObjectByType<ExperienceSystem>();
        encounterManager = FindFirstObjectByType<EncounterManager>();

        if (uiManager == null) Debug.LogError("❌ No BattleUIManager found!");
        if (expSystem == null) Debug.LogError("❌ No ExperienceSystem found!");
        if (encounterManager == null) Debug.LogError("❌ No EncounterManager found!");

        // Clean old spawned objects
        ClearSpawnedCharacters();

        // === SPAWN TEAMS ===
        SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
        SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

        // === Initialize EXP System AFTER persistence is applied ===
        expSystem.Initialize(playerControllers);

        battleActive = true;
        StartCoroutine(BattleLoop());

        Debug.Log($"✅ Battle started: {playerControllers.Count} players vs {enemyControllers.Count} enemies");
    }

    private void ClearSpawnedCharacters()
    {
        foreach (Transform t in playerSpawnPoints)
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);

        foreach (Transform t in enemySpawnPoints)
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);

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
    // 🌀 MAIN BATTLE LOOP
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
    // 🧠 PLAYER COMMAND PHASE
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

            uiManager.SetPlayerController(currentPlayer);
            uiManager.BeginPlayerChoice((attack, target) =>
            {
                chosenAttack = attack;
                chosenTarget = target;
                actionChosen = true;
            });

            yield return new WaitUntil(() => actionChosen);

            if (chosenAttack == null || chosenTarget == null)
            {
                var fallbackAttack = runtime.equippedAttacks.Count > 0 ? runtime.equippedAttacks[0] : null;
                var fallbackTarget = enemyControllers.Find(e => e.GetRuntimeCharacter().IsAlive);

                if (fallbackAttack != null && fallbackTarget != null)
                    chosenActions[currentPlayer] = (fallbackAttack, fallbackTarget);
            }
            else
            {
                chosenActions[currentPlayer] = (chosenAttack, chosenTarget);
            }

            playerChoiceIndex++;
        }

        yield return new WaitForSeconds(0.2f);
    }

    // ==========================================================
    // 🤖 ENEMY COMMAND PHASE
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
    // ⚔️ ACTION RESOLUTION
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
    // 💥 ATTACK EXECUTION
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
    // ✨ HELPERS
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

        if (encounterManager != null)
            encounterManager.EndEncounter();
    }
}
