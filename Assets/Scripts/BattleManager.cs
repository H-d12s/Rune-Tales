using UnityEngine;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    private ExperienceSystem expSystem;

    [Header("Spawn Points")]
    public Transform[] playerSpawnPoints;
    public Transform[] enemySpawnPoints;

    [Header("Prefabs")]
    public GameObject characterPrefab; // Used for both players & enemies

    [Header("Team Data")]
    public List<CharacterData> playerTeam;
    public List<CharacterData> enemyTeam;

    private List<CharacterBattleController> playerControllers = new List<CharacterBattleController>();
    private List<CharacterBattleController> enemyControllers = new List<CharacterBattleController>();

    private BattleUIManager uiManager;

    [Header("Spawn Offset")]
    public float verticalOffset = -1.5f;

    void Start()
    {
        uiManager = FindObjectOfType<BattleUIManager>();
        if (uiManager == null)
        {
            Debug.LogError("❌ No BattleUIManager found in the scene!");
            return;
        }

        // ✅ Get ExperienceSystem reference
        expSystem = FindObjectOfType<ExperienceSystem>();
        if (expSystem == null)
            Debug.LogError("❌ No ExperienceSystem found in the scene!");
        
        // Spawn both sides
        SpawnTeam(playerTeam, playerSpawnPoints, playerControllers, true);
        SpawnTeam(enemyTeam, enemySpawnPoints, enemyControllers, false);

        Debug.Log($"✅ Battle started: {playerControllers.Count} players vs {enemyControllers.Count} enemies");

        // Connect UI to first player
        if (playerControllers.Count > 0)
            uiManager.SetPlayerController(playerControllers[0]);

        // ✅ Initialize XP system with player controllers
        if (expSystem != null)
            expSystem.Initialize(playerControllers);
    }

    private void SpawnTeam(List<CharacterData> teamData, Transform[] spawnPoints,
        List<CharacterBattleController> controllerList, bool isPlayerTeam)
    {
        for (int i = 0; i < teamData.Count && i < spawnPoints.Length; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
                continue;

            Vector3 spawnPos = spawnPoint.position + new Vector3(0, verticalOffset, 0);
            GameObject characterObj = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
            characterObj.name = $"{(isPlayerTeam ? "Player" : "Enemy")}_{teamData[i].characterName}";

            var controller = characterObj.GetComponent<CharacterBattleController>();
            controller.characterData = teamData[i];
            controller.isPlayer = isPlayerTeam;
            controller.InitializeCharacter();

            // Enemy Selector setup
            var selector = characterObj.GetComponent<EnemySelector>();
            if (!selector) selector = characterObj.AddComponent<EnemySelector>();

            var collider = characterObj.GetComponent<Collider2D>();
            if (!collider) collider = characterObj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            if (isPlayerTeam)
            {
                selector.enabled = false;
                collider.enabled = false;
            }
            else
            {
                selector.enabled = true;
                collider.enabled = true;

                // Flip enemy sprite
                var scale = characterObj.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * -1;
                characterObj.transform.localScale = scale;
            }

            controllerList.Add(controller);
        }
    }

    /// <summary>
    /// Called by UI when an attack is performed.
    /// </summary>
    public void PerformAttack(CharacterBattleController attacker, CharacterBattleController target, AttackData attack)
    {
        if (attacker == null || target == null || attack == null)
        {
            Debug.LogError("❌ Invalid attack parameters!");
            return;
        }

        // --- 🎯 Base Damage Calculation ---
        int baseDamage = Mathf.Max(1, attack.power + attacker.GetRuntimeCharacter().Attack - target.GetRuntimeCharacter().Defense / 2);

        // --- 🎲 Dice Roll Mechanic ---
        int diceRoll = Random.Range(1, 11); // 1–10 inclusive
        float multiplier;

        if (diceRoll >= 8)
            multiplier = 1.25f; // Strong hit
        else if (diceRoll <= 3)
            multiplier = 0.75f; // Weak hit
        else
            multiplier = 1f;    // Normal

        int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

        // --- 💥 Apply Damage ---
        target.GetRuntimeCharacter().TakeDamage(finalDamage);

        // --- 🪄 Log Output ---
        string hitType = multiplier > 1f ? "💥 Critical Hit!" :
                         multiplier < 1f ? "🩹 Weak Hit!" :
                         "⚔️ Normal Hit!";
        Debug.Log($"🎲 Dice Roll: {diceRoll} → {hitType}");
        Debug.Log($"⚔️ {attacker.characterData.characterName} dealt {finalDamage} damage to {target.characterData.characterName} using {attack.attackName}");

        // --- 💀 Death Handling ---
        if (!target.GetRuntimeCharacter().IsAlive)
        {
            Debug.Log($"💀 {target.characterData.characterName} has been defeated!");

            // Disable selection & collider
            var selector = target.GetComponent<EnemySelector>();
            if (selector != null)
                selector.DisableSelection();

            var col = target.GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;

            // Start death coroutine (fade & destroy)
            var targetController = target.GetComponent<CharacterBattleController>();
            if (targetController != null)
                targetController.StartCoroutine(targetController.HandleDeath());

            // Remove from enemy list
            if (enemyControllers.Contains(target))
                enemyControllers.Remove(target);

            // --- 🧠 Award EXP ---
            if (attacker.isPlayer && expSystem != null)
            {
                expSystem.GrantXP(target.characterData);
            }
        }
    }
}
