using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles XP gain, level-ups, and stat growth for each character class.
/// Works with PersistentPlayerData to ensure progress carries across encounters.
/// Prompts for move replacements are queued during battle and processed after victory.
/// </summary>
public class ExperienceSystem : MonoBehaviour
{
    [Header("XP Curve Settings")]
    public int baseXPRequired = 50;
    public float growthRate = 1.2f;
    public int maxLevel = 30;

    [Header("Debug Info")]
    public int totalXPThisBattle;

    private List<CharacterBattleController> playerControllers;
    private static Dictionary<string, int> PlayerXPData = new Dictionary<string, int>();

    // Flag & queue for post-battle processing
    private bool isProcessingLevelUps = false;
    public bool IsProcessingLevelUps => isProcessingLevelUps;

    // Queue of (runtime, level) for each level the runtime reached during this battle
    private List<(CharacterRuntime runtime, int level)> pendingLevelUpQueue = new List<(CharacterRuntime, int)>();

    public void Initialize(List<CharacterBattleController> playerTeam)
    {
        playerControllers = playerTeam;
        totalXPThisBattle = 0;

        // Restore XP + Level data if stored in runtime dict
        if (PersistentPlayerData.Instance != null)
        {
            foreach (var controller in playerControllers)
            {
                var runtime = controller.GetRuntimeCharacter();
                if (PlayerXPData.ContainsKey(runtime.baseData.characterName))
                    Debug.Log($"♻️ Restored XP for {runtime.baseData.characterName}: {PlayerXPData[runtime.baseData.characterName]} XP");
            }
        }
    }

    public void GrantXP(CharacterData enemyData)
    {
        if (enemyData == null || playerControllers == null)
            return;

        int xpReward = enemyData.expReward;
        totalXPThisBattle += xpReward;

        Debug.Log($"⭐ Enemy defeated! Gained {xpReward} XP!");

        foreach (var controller in playerControllers)
        {
            if (controller == null || !controller.GetRuntimeCharacter().IsAlive)
                continue;

            AddXP(controller.GetRuntimeCharacter(), xpReward);
        }

        // Save after XP grant to persist progress
        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
    }

    private void AddXP(CharacterRuntime runtime, int amount)
    {
        if (runtime == null || runtime.currentLevel >= maxLevel)
            return;

        if (!PlayerXPData.ContainsKey(runtime.baseData.characterName))
            PlayerXPData[runtime.baseData.characterName] = 0;

        PlayerXPData[runtime.baseData.characterName] += amount;
        int currentXP = PlayerXPData[runtime.baseData.characterName];
        int xpToNext = GetXPToNextLevel(runtime.currentLevel);

        Debug.Log($"🧮 {runtime.baseData.characterName}: {currentXP}/{xpToNext} XP");

        while (currentXP >= xpToNext && runtime.currentLevel < maxLevel)
        {
            currentXP -= xpToNext;
            runtime.currentLevel++;
            xpToNext = GetXPToNextLevel(runtime.currentLevel);

            ApplyStatGrowth(runtime);
            Debug.Log($"⬆️ {runtime.baseData.characterName} leveled up! (Now Level {runtime.currentLevel})");
            ShowLevelUpPopup(runtime.baseData.characterName, runtime.currentLevel);

            // Queue this exact level for post-battle processing (so we only check moves unlocked at that level)
            pendingLevelUpQueue.Add((runtime, runtime.currentLevel));

            // DO NOT call the move prompt here — we process them after the battle.
        }

        PlayerXPData[runtime.baseData.characterName] = currentXP;

        // Update persistent data immediately
        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
    }

    private void ApplyStatGrowth(CharacterRuntime runtime)
    {
        var data = runtime.baseData;
        string tag = data.characterTag.ToLower();

        float hpGrowth = 0.1f;
        float atkGrowth = 0.08f;
        float defGrowth = 0.07f;
        float spdGrowth = 0.05f;

        switch (tag)
        {
            case "warrior":
                hpGrowth = 0.2f; atkGrowth = 0.15f; defGrowth = 0.1f; spdGrowth = 0.03f;
                break;
            case "mage":
                hpGrowth = 0.1f; atkGrowth = 0.2f; defGrowth = 0.05f; spdGrowth = 0.05f;
                break;
            case "rogue":
                hpGrowth = 0.1f; atkGrowth = 0.12f; defGrowth = 0.05f; spdGrowth = 0.15f;
                break;
            case "tank":
                hpGrowth = 0.25f; atkGrowth = 0.08f; defGrowth = 0.15f; spdGrowth = 0.02f;
                break;
        }

        runtime.runtimeHP = Mathf.RoundToInt(runtime.runtimeHP * (1 + hpGrowth));
        runtime.runtimeAttack = Mathf.RoundToInt(runtime.runtimeAttack * (1 + atkGrowth));
        runtime.runtimeDefense = Mathf.RoundToInt(runtime.runtimeDefense * (1 + defGrowth));
        runtime.runtimeSpeed = Mathf.RoundToInt(runtime.runtimeSpeed * (1 + spdGrowth));

        runtime.currentHP = runtime.runtimeHP;

        Debug.Log($"📈 {data.characterName} stats increased (runtime only)!");
        Debug.Log($"HP: {runtime.runtimeHP}, ATK: {runtime.runtimeAttack}, DEF: {runtime.runtimeDefense}, SPD: {runtime.runtimeSpeed}");
    }

    /// <summary>
    /// Called by BattleManager after victory to process all queued level-up move prompts
    /// in order, pausing the game until the player finishes each prompt.
    /// </summary>
    public IEnumerator ProcessPendingMovePrompts()
    {
        // Guard
        if (pendingLevelUpQueue == null || pendingLevelUpQueue.Count == 0)
            yield break;

        isProcessingLevelUps = true;

        // Iterate over a copy so other code can safely modify the original list if needed
        var queueCopy = new List<(CharacterRuntime runtime, int level)>(pendingLevelUpQueue);

        foreach (var entry in queueCopy)
        {
            var runtime = entry.runtime;
            var level = entry.level;
            if (runtime == null) continue;

            // Process moves that unlocked *exactly* at this level
            yield return StartCoroutine(LearnNewAttacksAtLevelCoroutine(runtime, level));
        }

        // Clear queue after processing
        pendingLevelUpQueue.Clear();
        isProcessingLevelUps = false;
        yield break;
    }

    // ============================
    // Move-learn logic for a specific level (only new moves that unlock at that level)
    // ============================
    private IEnumerator LearnNewAttacksAtLevelCoroutine(CharacterRuntime runtime, int level)
    {
        if (runtime == null || runtime.baseData == null)
            yield break;

        // Get moves available at this level and at previous level (if any)
        var attacksAtLevel = runtime.baseData.GetAvailableAttacks(level) ?? new List<AttackData>();
        var attacksPrev = level > 1 ? runtime.baseData.GetAvailableAttacks(level - 1) ?? new List<AttackData>() : new List<AttackData>();

        // Build a set (by name) of previous-level attacks for robust comparison
        var prevNames = new HashSet<string>();
        foreach (var a in attacksPrev)
            if (a != null && !string.IsNullOrEmpty(a.attackName))
                prevNames.Add(a.attackName);

        // Determine truly new attacks unlocked at this level (by name)
        var newAttacks = new List<AttackData>();
        foreach (var a in attacksAtLevel)
        {
            if (a == null || string.IsNullOrEmpty(a.attackName))
                continue;

            if (prevNames.Contains(a.attackName))
                continue; // already available previously

            // Also skip if runtime already knows it (defensive)
            bool alreadyKnown = false;
            if (runtime.equippedAttacks != null)
            {
                foreach (var known in runtime.equippedAttacks)
                {
                    if (known != null && known.attackName == a.attackName) { alreadyKnown = true; break; }
                }
            }
            if (alreadyKnown) continue;

            newAttacks.Add(a);
        }

        // If nothing new at this level, don't prompt — just return
        if (newAttacks.Count == 0)
            yield break;

        // For each newly unlocked attack, prompt the player (replace/cancel) — sequentially
        foreach (var newAttack in newAttacks)
        {
            if (runtime.equippedAttacks.Contains(newAttack))
                continue;

            if (runtime.equippedAttacks.Count < 2)
            {
                runtime.equippedAttacks.Add(newAttack);
                ShowLearnAttackPopup(runtime.baseData.characterName, newAttack.attackName);
                if (PersistentPlayerData.Instance != null)
                    PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
                yield return null;
            }
            else
            {
                // Use your existing coroutine-based prompt that waits

                // Wait until the player finishes the replace/cancel choice
                yield return StartCoroutine(PromptMoveReplaceCoroutine(runtime, newAttack));
            }
        }
    }

    private IEnumerator PromptMoveReplaceCoroutine(CharacterRuntime runtime, AttackData newAttack)
    {
        Debug.Log($"🧠 {runtime.baseData.characterName} wants to learn {newAttack.attackName}, but already knows {runtime.equippedAttacks.Count} moves!");

        var moveNames = new List<string>();
        foreach (var atk in runtime.equippedAttacks)
            moveNames.Add(atk.attackName);

        if (MoveReplaceUIManager.Instance != null)
        {
            MoveReplaceUIManager.Instance.ShowReplacePrompt(moveNames, newAttack.attackName, null, null);

            // Wait until the UI manager finishes (user chose or cancelled)
            yield return new WaitUntil(() => !MoveReplaceUIManager.Instance.IsAwaitingChoice);

            // If cancelled
            if (MoveReplaceUIManager.Instance.WasCancelled)
            {
                Debug.Log($"🕊️ {runtime.baseData.characterName} decided NOT to learn {newAttack.attackName}.");
                yield break;
            }

            int chosenIndex = MoveReplaceUIManager.Instance.LastSelectedIndex;
            if (chosenIndex < 0 || chosenIndex >= runtime.equippedAttacks.Count)
            {
                Debug.LogWarning("⚠️ Invalid move index chosen. Aborting learn.");
                yield break;
            }

            var oldAttack = runtime.equippedAttacks[chosenIndex];
            runtime.equippedAttacks[chosenIndex] = newAttack;

            Debug.Log($"🔄 {runtime.baseData.characterName} forgot {oldAttack.attackName} and learned {newAttack.attackName}!");
            ShowLearnAttackPopup(runtime.baseData.characterName, newAttack.attackName);

            // Persist the change immediately
            if (PersistentPlayerData.Instance != null)
                PersistentPlayerData.Instance.UpdateFromRuntime(runtime);

            yield return null;
        }
        else
        {
            // Fallback: auto-replace first move
            Debug.LogWarning("⚠️ MoveReplaceUIManager not found, auto-replacing first move instead.");
            if (runtime.equippedAttacks.Count > 0)
            {
                var oldAttack = runtime.equippedAttacks[0];
                runtime.equippedAttacks[0] = newAttack;

                Debug.Log($"🔄 {runtime.baseData.characterName} forgot {oldAttack.attackName} and learned {newAttack.attackName}!");
                ShowLearnAttackPopup(runtime.baseData.characterName, newAttack.attackName);

                if (PersistentPlayerData.Instance != null)
                    PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
            }

            yield return null;
        }
    }

    private void ShowLevelUpPopup(string charName, int newLevel)
    {
        Debug.Log($"🎉 {charName} reached Level {newLevel}!");
    }

    private void ShowLearnAttackPopup(string charName, string attackName)
    {
        Debug.Log($"🔥 {charName} learned a new attack: {attackName}!");
    }

    public int GetXPToNextLevel(int currentLevel)
    {
        return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(growthRate, currentLevel - 1));
    }
}
