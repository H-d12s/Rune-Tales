using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
/// <summary>
/// Handles XP gain, level-ups, stat growth and queues move-learn prompts.
/// Adjusted so prompts are grouped per-character and UI behavior is robust.
/// Now: level-up & move-learn notifications are shown via LevelUpUI (separate panel).
/// </summary>
public class ExperienceSystem : MonoBehaviour
{
    [Header("XP Curve Settings")]
    public int baseXPRequired = 50;
    public float growthRate = 1.2f;

    public LevelUpUI levelUpUI;


    private BattleManager battleManager;

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

    // Event fired when stored XP for a character changes: (characterName, storedXP)
    public event Action<string, int> OnXPUpdated;

    #region Public API helpers

    public int GetStoredXPFor(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return 0;
        if (PlayerXPData.TryGetValue(characterName, out int xp)) return xp;
        return 0;
    }

    public int GetXPToNextLevel(int currentLevel)
    {
        int lvl = Mathf.Max(1, currentLevel);
        return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(growthRate, lvl - 1));
    }

    /// <summary>
    /// Called by BattleManager after victory to process all queued level-up move prompts
    /// in order, pausing the game until the player finishes each prompt.
    /// NOTE: we group prompts by runtime so a character that leveled multiple times
    /// is handled in a single contiguous sequence of prompts.
    /// </summary>
    public IEnumerator ProcessPendingMovePrompts()
    {
        if (pendingLevelUpQueue == null || pendingLevelUpQueue.Count == 0)
            yield break;

        isProcessingLevelUps = true;

        // Group queued entries by runtime so we handle all levels for a single character at once
        var grouped = new Dictionary<CharacterRuntime, List<int>>();
        foreach (var entry in pendingLevelUpQueue)
        {
            if (entry.runtime == null) continue;
            if (!grouped.ContainsKey(entry.runtime)) grouped[entry.runtime] = new List<int>();
            grouped[entry.runtime].Add(entry.level);
        }

        // Sort levels for each runtime (ascending) and process characters in the order they were queued
        foreach (var kv in grouped)
        {
            var runtime = kv.Key;
            var levels = kv.Value;
            levels.Sort();

            // Process each level for this runtime (keeps prompts contiguous per character)
            foreach (int level in levels)
            {
                // small frame delay to ensure UI updates between characters
                yield return null;
                yield return StartCoroutine(LearnNewAttacksAtLevelCoroutine(runtime, level));

                // brief pause to let messages finish/hide before next prompt starts
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        // Clear queue after processing
        pendingLevelUpQueue.Clear();
        isProcessingLevelUps = false;
        yield break;
    }

    #endregion

   public void Initialize(List<CharacterBattleController> playerTeam, BattleManager manager)
{
    playerControllers = playerTeam;
    battleManager = manager;
    totalXPThisBattle = 0;

    // Try to use inspector assignment first (if you manually set it)
    if (levelUpUI == null)
    {
        // First try the normal FindObjectOfType (will only find active objects)
        levelUpUI = FindObjectOfType<LevelUpUI>();

        // If not found, also search all loaded objects (this includes disabled objects & prefabs)
        if (levelUpUI == null)
        {
            var all = Resources.FindObjectsOfTypeAll<LevelUpUI>();
            if (all != null && all.Length > 0)
                levelUpUI = all.First();
        }
    }

    Debug.Log($"[ExperienceSystem] LevelUpUI resolved: {(levelUpUI != null ? "FOUND" : "NOT FOUND")}");

    if (PersistentPlayerData.Instance != null)
    {
        foreach (var controller in playerControllers)
        {
            var runtime = controller.GetRuntimeCharacter();
            if (runtime == null) continue;
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
            if (controller == null || controller.GetRuntimeCharacter() == null || !controller.GetRuntimeCharacter().IsAlive)
                continue;

            AddXP(controller.GetRuntimeCharacter(), xpReward);
        }

        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
    }

    private void AddXP(CharacterRuntime runtime, int amount)
    {
        if (runtime == null || runtime.currentLevel >= maxLevel)
            return;

        string name = runtime.baseData.characterName;
        if (string.IsNullOrEmpty(name)) return;

        if (!PlayerXPData.ContainsKey(name))
            PlayerXPData[name] = 0;

        PlayerXPData[name] += amount;
        int currentXP = PlayerXPData[name];
        int xpToNext = GetXPToNextLevel(runtime.currentLevel);

        OnXPUpdated?.Invoke(name, currentXP);

        Debug.Log($"🧮 {name}: {currentXP}/{xpToNext} XP");

        while (currentXP >= xpToNext && runtime.currentLevel < maxLevel)
        {
            currentXP -= xpToNext;
            runtime.currentLevel++;
            xpToNext = GetXPToNextLevel(runtime.currentLevel);

            ApplyStatGrowth(runtime);
            Debug.Log($"⬆️ {name} leveled up! (Now Level {runtime.currentLevel})");

            // Use LevelUpUI if available, otherwise fallback to BattleManager queue
            ShowLevelUpPopup(name, runtime.currentLevel);

            // Queue this exact level for post-battle processing (moves unlocked at that level)
            pendingLevelUpQueue.Add((runtime, runtime.currentLevel));
        }

        PlayerXPData[name] = currentXP;

        OnXPUpdated?.Invoke(name, currentXP);

        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
    }

    private void ApplyStatGrowth(CharacterRuntime runtime)
    {
        if (runtime == null || runtime.baseData == null) return;

        var data = runtime.baseData;
        string tag = data.characterTag?.ToLower() ?? "";

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
            case "archer":
                hpGrowth = 0.1f; atkGrowth = 0.12f; defGrowth = 0.05f; spdGrowth = 0.12f;
                break;
            case "assassin":
                hpGrowth = 0.25f; atkGrowth = 0.08f; defGrowth = 0.05f; spdGrowth = 0.12f;
                break;
            case "pyromancer":
                hpGrowth = 0.2f; atkGrowth = 0.09f; defGrowth = 0.15f; spdGrowth = 0.12f;
                break;
            case "gunslinger":
                hpGrowth = 0.2f; atkGrowth = 0.09f; defGrowth = 0.15f; spdGrowth = 0.12f;
                break;
            case "priest":
                hpGrowth = 0.2f; atkGrowth = 0.09f; defGrowth = 0.15f; spdGrowth = 0.12f;
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

    // ============================
    // Move-learn logic for a specific level (only new moves that unlock at that level)
    // Modified: collect learn messages and flush them through LevelUpUI so they do not touch BattleMessageUI
    // ============================
    private IEnumerator LearnNewAttacksAtLevelCoroutine(CharacterRuntime runtime, int level)
    {
        if (runtime == null || runtime.baseData == null)
            yield break;

        var attacksAtLevel = runtime.baseData.GetAvailableAttacks(level) ?? new List<AttackData>();
        var attacksPrev = level > 1 ? runtime.baseData.GetAvailableAttacks(level - 1) ?? new List<AttackData>() : new List<AttackData>();

        var prevNames = new HashSet<string>();
        foreach (var a in attacksPrev)
            if (a != null && !string.IsNullOrEmpty(a.attackName))
                prevNames.Add(a.attackName);

        var newAttacks = new List<AttackData>();
        foreach (var a in attacksAtLevel)
        {
            if (a == null || string.IsNullOrEmpty(a.attackName)) continue;
            if (prevNames.Contains(a.attackName)) continue;

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

        if (newAttacks.Count == 0) yield break;

        // Local list to accumulate learn-notification messages for this runtime/level.
        var learnMessages = new List<string>();

        // Helper: restore Battle UI for this runtime (if a controller exists)
        Action restoreUIForRuntime = () =>
        {
            try
            {
                var ui = FindObjectOfType<BattleUIManager>();
                if (ui == null) return;

                CharacterBattleController foundController = null;
                var allCtrls = FindObjectsOfType<CharacterBattleController>();
                foreach (var c in allCtrls)
                {
                    if (c == null) continue;
                    var r = c.GetRuntimeCharacter();
                    if (r == runtime)
                    {
                        foundController = c;
                        break;
                    }
                }

                if (foundController != null)
                {
                    try { ui.SetPlayerController(foundController); } catch { }
                    try { ui.ShowMainActions(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Restore UI failed: {ex}");
            }
        };

        foreach (var newAttack in newAttacks)
        {
            if (newAttack == null) continue;

            bool knownByName = false;
            if (runtime.equippedAttacks != null)
            {
                foreach (var k in runtime.equippedAttacks)
                    if (k != null && k.attackName == newAttack.attackName) { knownByName = true; break; }
            }
            if (knownByName) continue;

            // If there's room, auto-learn (accumulate message)
            if (runtime.equippedAttacks == null || runtime.equippedAttacks.Count < 2)
            {
                if (runtime.equippedAttacks == null)
                    runtime.equippedAttacks = new List<AttackData>();

                runtime.equippedAttacks.Add(newAttack);

                // Accumulate message (we'll flush after processing all newAttacks for this runtime/level)
                var who = runtime.baseData?.characterName ?? "Unknown";
                learnMessages.Add($"{who} learned a new attack: {newAttack.attackName}!");

                if (PersistentPlayerData.Instance != null)
                    PersistentPlayerData.Instance.UpdateFromRuntime(runtime);

                // Restore the battle UI so the attack buttons reappear (important for auto-learn)
                restoreUIForRuntime();

                yield return new WaitForSecondsRealtime(0.05f);
            }
            else
            {
                // Full move list — hand off to the prompt manager which will return when finished.
                // We pass learnMessages so the prompt coroutine can append to it when an attack is learned.
                yield return StartCoroutine(PromptMoveReplaceCoroutine(runtime, newAttack, learnMessages));

                // After the player finishes (either replaced or cancelled), restore the battle UI
                restoreUIForRuntime();

                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        // After processing all newAttacks for this runtime/level, flush accumulated learn messages
        if (learnMessages.Count > 0)
        {
            if (levelUpUI != null)
            {
                // non-blocking sequence via LevelUpUI so the flow looks identical to level-up popups
                yield return StartCoroutine(levelUpUI.ShowSequenceNonBlocking(learnMessages, levelUpUI.defaultAutoHideSeconds));
            }
            else if (battleManager != null)
            {
                foreach (var msg in learnMessages)
                {
                    yield return new WaitForSecondsRealtime(0.08f);
                    // fallback to BattleManager queue non-blocking
                    yield return StartCoroutine(battleManager.ShowBattleMessage(msg, false));
                }
            }
            else
            {
                foreach (var msg in learnMessages) Debug.Log(msg);
            }
        }
    }

    // Modified PromptMoveReplaceCoroutine to accept a learnMessages list to append to
    private IEnumerator PromptMoveReplaceCoroutine(CharacterRuntime runtime, AttackData newAttack, List<string> learnMessages)
    {
        if (runtime == null || runtime.baseData == null || newAttack == null)
            yield break;

        // build list of equipped move names for display
        var moveNames = new List<string>();
        if (runtime.equippedAttacks != null)
        {
            foreach (var a in runtime.equippedAttacks)
                moveNames.Add(a != null ? a.attackName : "(unknown)");
        }

        // Show the simple persistent prompt (MoveReplaceUIManager will keep the panel visible)
        if (MoveReplaceUIManager.Instance != null)
        {
            MoveReplaceUIManager.Instance.ShowReplacePrompt(runtime.baseData.characterName, moveNames, newAttack.attackName);

            // Wait until player chooses or cancels
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

            Debug.Log($"🔄 {runtime.baseData.characterName} forgot {oldAttack?.attackName ?? "(unknown)"} and learned {newAttack.attackName}!");

            // Instead of showing the message immediately, append to the learnMessages list
            var who = runtime.baseData?.characterName ?? "Unknown";
            learnMessages.Add($"{who} learned a new attack: {newAttack.attackName}!");

            if (PersistentPlayerData.Instance != null)
                PersistentPlayerData.Instance.UpdateFromRuntime(runtime);

            yield return null;
        }
        else
        {
            Debug.LogWarning("⚠️ MoveReplaceUIManager not found — auto-replacing first move.");
            if (runtime.equippedAttacks != null && runtime.equippedAttacks.Count > 0)
            {
                var oldAttack = runtime.equippedAttacks[0];
                runtime.equippedAttacks[0] = newAttack;
                Debug.Log($"🔄 {runtime.baseData.characterName} forgot {oldAttack?.attackName ?? "(unknown)"} and learned {newAttack.attackName}!");

                var who = runtime.baseData?.characterName ?? "Unknown";
                learnMessages.Add($"{who} learned a new attack: {newAttack.attackName}!");

                if (PersistentPlayerData.Instance != null) PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
            }
            yield return null;
        }
    }

    // Use LevelUpUI or BattleManager's message queue when possible (non-blocking notification)
   private void ShowLevelUpPopup(string charName, int newLevel)
{
    string msg = $"{charName} reached Level {newLevel}!";
    if (levelUpUI != null)
    {
        // non-blocking via LevelUpUI (so it doesn't block processing here)
        levelUpUI.StartCoroutine(levelUpUI.ShowNonBlocking("Level Up!", msg, levelUpUI.defaultAutoHideSeconds));
    }
    else if (battleManager != null)
    {
        Debug.LogWarning("[ExperienceSystem] LevelUpUI not found - falling back to BattleManager messages.");
        battleManager.StartCoroutine(battleManager.ShowBattleMessage(msg, false));
    }
    else
    {
        Debug.Log($"🎉 {msg}");
    }
}

}
