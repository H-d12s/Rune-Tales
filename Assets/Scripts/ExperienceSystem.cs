using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles XP gain, level-ups, stat growth and queued move-learn notifications.
/// - Interactive move-replace prompts have been removed. When a new move is learned
///   and the equipped list is full, the first move (index 0) is automatically replaced.
/// - Level-up & move-learn notifications are shown via LevelUpUI when available,
///   otherwise fallback to BattleManager's ShowBattleMessage queue.
/// </summary>
public class ExperienceSystem : MonoBehaviour
{
    [Header("XP Curve Settings")]
    public int baseXPRequired = 50;
    public float growthRate = 1.2f;

    [Header("UI")]
    public LevelUpUI levelUpUI;

    [Header("Gameplay")]
    public int maxLevel = 30;

    [Header("Debug")]
    public int totalXPThisBattle;

    private BattleManager battleManager;
    private List<CharacterBattleController> playerControllers;

    // persistent stored XP across sessions (characterName -> xp)
    private static Dictionary<string, int> PlayerXPData = new Dictionary<string, int>();

    // pending levels (runtime, level) recorded while in battle; processed after battle
    private List<(CharacterRuntime runtime, int level)> pendingLevelUpQueue = new List<(CharacterRuntime, int)>();

    private bool isProcessingLevelUps = false;
    public bool IsProcessingLevelUps => isProcessingLevelUps;

    // event when stored XP changes
    public event Action<string, int> OnXPUpdated;

    #region Public API

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
    /// Initialize ExperienceSystem once a battle begins.
    /// Assigns the player team and battle manager and resolves LevelUpUI if not set.
    /// </summary>
    public void Initialize(List<CharacterBattleController> playerTeam, BattleManager manager)
    {
        playerControllers = playerTeam;
        battleManager = manager;
        totalXPThisBattle = 0;

        if (levelUpUI == null)
        {
            // prefer active scene object
            levelUpUI = FindObjectOfType<LevelUpUI>();

            // fallback: find in all loaded objects (may include disabled prefabs)
            if (levelUpUI == null)
            {
                var all = Resources.FindObjectsOfTypeAll<LevelUpUI>();
                if (all != null && all.Length > 0) levelUpUI = all.First();
            }
        }

        Debug.Log($"[ExperienceSystem] LevelUpUI resolved: {(levelUpUI != null ? "FOUND" : "NOT FOUND")}");
    }

    /// <summary>
    /// Called by BattleManager when an enemy is defeated to grant XP to players.
    /// </summary>
    public void GrantXP(CharacterData enemyData)
    {
        if (enemyData == null || playerControllers == null) return;

        int xpReward = enemyData.expReward;
        totalXPThisBattle += xpReward;
        Debug.Log($"⭐ Enemy defeated! Gained {xpReward} XP!");

        foreach (var controller in playerControllers)
        {
            if (controller == null) continue;
            var runtime = controller.GetRuntimeCharacter();
            if (runtime == null || !runtime.IsAlive) continue;
            AddXP(runtime, xpReward);
        }

        if (PersistentPlayerData.Instance != null)
            PersistentPlayerData.Instance.SaveAllPlayers(playerControllers);
    }

    #endregion

    #region XP / Level logic

    private void AddXP(CharacterRuntime runtime, int amount)
    {
        if (runtime == null || runtime.currentLevel >= maxLevel) return;
        string name = runtime.baseData?.characterName;
        if (string.IsNullOrEmpty(name)) return;

        if (!PlayerXPData.ContainsKey(name)) PlayerXPData[name] = 0;

        PlayerXPData[name] += amount;
        int currentXP = PlayerXPData[name];
        int xpToNext = GetXPToNextLevel(runtime.currentLevel);

        OnXPUpdated?.Invoke(name, currentXP);
        Debug.Log($"🧮 {name}: {currentXP}/{xpToNext} XP");

        // handle possibly multiple level-ups from a single XP gain
        while (currentXP >= xpToNext && runtime.currentLevel < maxLevel)
        {
            currentXP -= xpToNext;
            runtime.currentLevel++;
            xpToNext = GetXPToNextLevel(runtime.currentLevel);

            ApplyStatGrowth(runtime);
            Debug.Log($"⬆️ {name} leveled up! (Now Level {runtime.currentLevel})");

            // show a quick level-up notification
            ShowLevelUpPopup(name, runtime.currentLevel);

            // queue this level for post-battle processing (e.g., learn moves unlocked at this level)
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
        string tag = (data.characterTag ?? "").ToLowerInvariant();

        // default growth rates
        float hpGrowth = 0.10f;
        float atkGrowth = 0.08f;
        float defGrowth = 0.07f;
        float spdGrowth = 0.05f;

        switch (tag)
        {
            case "warrior":
                hpGrowth = 0.20f; atkGrowth = 0.15f; defGrowth = 0.10f; spdGrowth = 0.03f;
                break;
            case "mage":
                hpGrowth = 0.10f; atkGrowth = 0.20f; defGrowth = 0.05f; spdGrowth = 0.05f;
                break;
            case "rogue":
                hpGrowth = 0.10f; atkGrowth = 0.12f; defGrowth = 0.05f; spdGrowth = 0.15f;
                break;
            case "tank":
                hpGrowth = 0.25f; atkGrowth = 0.08f; defGrowth = 0.15f; spdGrowth = 0.02f;
                break;
            case "archer":
                hpGrowth = 0.10f; atkGrowth = 0.12f; defGrowth = 0.05f; spdGrowth = 0.12f;
                break;
            // add extra classes if needed
        }

        runtime.runtimeHP = Mathf.Max(1, Mathf.RoundToInt(runtime.runtimeHP * (1f + hpGrowth)));
        runtime.runtimeAttack = Mathf.Max(0, Mathf.RoundToInt(runtime.runtimeAttack * (1f + atkGrowth)));
        runtime.runtimeDefense = Mathf.Max(0, Mathf.RoundToInt(runtime.runtimeDefense * (1f + defGrowth)));
        runtime.runtimeSpeed = Mathf.Max(0, Mathf.RoundToInt(runtime.runtimeSpeed * (1f + spdGrowth)));

        runtime.currentHP = runtime.runtimeHP;

        Debug.Log($"📈 {data.characterName} stats increased (runtime): HP {runtime.runtimeHP}, ATK {runtime.runtimeAttack}, DEF {runtime.runtimeDefense}, SPD {runtime.runtimeSpeed}");
    }

    #endregion

    #region Move learn processing (auto-replace first move)

    /// <summary>
    /// Process all pending queued level-ups after the battle ends.
    /// Groups by runtime and processes levels for each runtime in the order they were queued.
    /// </summary>
    public IEnumerator ProcessPendingMovePrompts()
    {
        if (pendingLevelUpQueue == null || pendingLevelUpQueue.Count == 0)
            yield break;

        isProcessingLevelUps = true;

        // Preserve the order of runtimes as they were queued.
        var grouped = new Dictionary<CharacterRuntime, List<int>>();
        var runtimeOrder = new List<CharacterRuntime>();

        foreach (var entry in pendingLevelUpQueue)
        {
            if (entry.runtime == null) continue;
            if (!grouped.ContainsKey(entry.runtime))
            {
                grouped[entry.runtime] = new List<int>();
                runtimeOrder.Add(entry.runtime);
            }
            grouped[entry.runtime].Add(entry.level);
        }

        // Process in original order
        foreach (var runtime in runtimeOrder)
        {
            if (!grouped.TryGetValue(runtime, out var levels)) continue;
            levels.Sort();

            foreach (var lvl in levels)
            {
                // small frame to allow UI updating
                yield return null;
                yield return StartCoroutine(LearnNewAttacksAtLevelCoroutine(runtime, lvl));
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        pendingLevelUpQueue.Clear();
        isProcessingLevelUps = false;
        yield break;
    }

    /// <summary>
    /// For a single runtime and level, determine attacks that unlock at that level.
    /// If the runtime's equipped list has space, append. If it's full, automatically replace index 0.
    /// Collected messages are displayed via LevelUpUI (or BattleManager fallback).
    /// </summary>
    private IEnumerator LearnNewAttacksAtLevelCoroutine(CharacterRuntime runtime, int level)
    {
        if (runtime == null || runtime.baseData == null) yield break;

        var attacksAtLevel = runtime.baseData.GetAvailableAttacks(level) ?? new List<AttackData>();
        var attacksPrev = (level > 1) ? runtime.baseData.GetAvailableAttacks(level - 1) ?? new List<AttackData>() : new List<AttackData>();

        var prevNames = new HashSet<string>(attacksPrev.Where(a => a != null).Select(a => a.attackName));

        var newAttacks = new List<AttackData>();
        foreach (var a in attacksAtLevel)
        {
            if (a == null) continue;
            if (string.IsNullOrEmpty(a.attackName)) continue;
            if (prevNames.Contains(a.attackName)) continue;

            // skip if already known by runtime
            bool alreadyKnown = runtime.equippedAttacks?.Any(k => k != null && k.attackName == a.attackName) ?? false;
            if (alreadyKnown) continue;

            newAttacks.Add(a);
        }

        if (newAttacks.Count == 0) yield break;

        List<string> learnMessages = new List<string>();

        // helper: try to refresh battle UI so players see updated moves immediately
        Action restoreUIForRuntime = () =>
        {
            try
            {
                var ui = FindObjectOfType<BattleUIManager>();
                if (ui == null) return;

                CharacterBattleController foundController = null;
                foreach (var c in FindObjectsOfType<CharacterBattleController>())
                {
                    if (c == null) continue;
                    if (c.GetRuntimeCharacter() == runtime) { foundController = c; break; }
                }

                if (foundController != null)
                {
                    try { ui.SetPlayerController(foundController); } catch { }
                    try { ui.ShowMainActions(); } catch { }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"Restore UI failed: {ex}"); }
        };

        // Ensure equippedAttacks list exists
        if (runtime.equippedAttacks == null) runtime.equippedAttacks = new List<AttackData>();

        foreach (var newAttack in newAttacks)
        {
            if (newAttack == null) continue;

            // If there's room, learn directly
            if (runtime.equippedAttacks.Count < 2)
            {
                runtime.equippedAttacks.Add(newAttack);
                learnMessages.Add($"{runtime.baseData.characterName} learned a new attack: {newAttack.attackName}!");

                if (PersistentPlayerData.Instance != null)
                    PersistentPlayerData.Instance.UpdateFromRuntime(runtime);

                restoreUIForRuntime();
                yield return new WaitForSecondsRealtime(0.05f);
                continue;
            }

            // Otherwise: AUTO-REPLACE the first move (index 0)
            var oldAttack = runtime.equippedAttacks[0];
            runtime.equippedAttacks[0] = newAttack;

            learnMessages.Add($"{runtime.baseData.characterName} forgot {oldAttack?.attackName ?? "(unknown)"} and learned {newAttack.attackName}!");

            if (PersistentPlayerData.Instance != null)
                PersistentPlayerData.Instance.UpdateFromRuntime(runtime);

            restoreUIForRuntime();
            yield return new WaitForSecondsRealtime(0.05f);
        }

        // Display accumulated learn messages (non-blocking)
        if (learnMessages.Count > 0)
        {
            if (levelUpUI != null)
            {
                yield return StartCoroutine(levelUpUI.ShowSequenceNonBlocking(learnMessages, levelUpUI.defaultAutoHideSeconds));
            }
            else if (battleManager != null)
            {
                foreach (var msg in learnMessages)
                {
                    yield return new WaitForSecondsRealtime(0.08f);
                    yield return StartCoroutine(battleManager.ShowBattleMessage(msg, false));
                }
            }
            else
            {
                foreach (var msg in learnMessages) Debug.Log(msg);
            }
        }
    }

    #endregion

    #region ShowLevelUpPopup helper (fix for missing method error)

    /// <summary>
    /// Small helper to display a quick level-up notification. Uses LevelUpUI if available,
    /// otherwise falls back to BattleManager ShowBattleMessage (non-blocking) or Debug.Log.
    /// </summary>
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
            battleManager.StartCoroutine(battleManager.ShowBattleMessage(msg, false));
        }
        else
        {
            Debug.Log($"🎉 {msg}");
        }
    }

    #endregion

    #region Utilities / Debug

    /// <summary>
    /// Forcibly set stored XP for a character (debug / external callers).
    /// </summary>
    public void SetStoredXP(string characterName, int xp)
    {
        if (string.IsNullOrEmpty(characterName)) return;
        PlayerXPData[characterName] = Mathf.Max(0, xp);
        OnXPUpdated?.Invoke(characterName, PlayerXPData[characterName]);
    }

    #endregion
}
