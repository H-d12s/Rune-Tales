using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Handles XP gain, level-ups, and stat growth for each character class.
/// Works with PersistentPlayerData to ensure progress carries across encounters.
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

    // --- NEW FIELDS ---
    public bool moveLearningInProgress = false;

    // Queue to serialize move-learn prompts so only one prompt is active at a time.
    private Queue<(CharacterRuntime runtime, AttackData newAttack)> moveLearnQueue = new Queue<(CharacterRuntime, AttackData)>();
    private Coroutine queueProcessorCoroutine = null;

    public void Initialize(List<CharacterBattleController> playerTeam)
    {
        playerControllers = playerTeam;
        totalXPThisBattle = 0;

        // 🧠 Restore XP + Level data from persistent save (debug/logging)
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

        // ✅ Save after XP grant to persist progress
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

            LearnNewAttacks(runtime);
        }

        PlayerXPData[runtime.baseData.characterName] = currentXP;

        // 🧩 Update persistent data immediately
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

    private void LearnNewAttacks(CharacterRuntime runtime)
    {
        var availableAttacks = runtime.baseData.GetAvailableAttacks(runtime.currentLevel);

        foreach (var newAttack in availableAttacks)
        {
            if (runtime.equippedAttacks.Contains(newAttack))
                continue;

            if (runtime.equippedAttacks.Count < 2)
            {
                runtime.equippedAttacks.Add(newAttack);
                ShowLearnAttackPopup(runtime.baseData.characterName, newAttack.attackName);

                // Persist immediate if needed
                if (PersistentPlayerData.Instance != null)
                    PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
            }
            else
            {
                // ENQUEUE instead of trying to prompt immediately.
                EnqueueMoveLearn(runtime, newAttack);
            }
        }
    }

    private void EnqueueMoveLearn(CharacterRuntime runtime, AttackData newAttack)
    {
        // Add to queue
        moveLearnQueue.Enqueue((runtime, newAttack));

        // Start processing queue if not already
        if (queueProcessorCoroutine == null)
            queueProcessorCoroutine = StartCoroutine(ProcessMoveLearnQueue());
    }

    private IEnumerator ProcessMoveLearnQueue()
    {
        moveLearningInProgress = true;

        while (moveLearnQueue.Count > 0)
        {
            var (runtime, newAttack) = moveLearnQueue.Dequeue();

            bool decisionMade = false;

            // Prepare a local handler to apply a replacement (used by UI callback)
            System.Action<int> onReplace = (replaceIndex) =>
            {
                // Safety: ensure index valid
                if (replaceIndex >= 0 && replaceIndex < runtime.equippedAttacks.Count)
                {
                    var oldAttack = runtime.equippedAttacks[replaceIndex];
                    runtime.equippedAttacks[replaceIndex] = newAttack;

                    Debug.Log($"🔄 {runtime.baseData.characterName} forgot {oldAttack.attackName} and learned {newAttack.attackName}!");
                    ShowLearnAttackPopup(runtime.baseData.characterName, newAttack.attackName);

                    if (PersistentPlayerData.Instance != null)
                        PersistentPlayerData.Instance.UpdateFromRuntime(runtime);
                }
                else
                {
                    Debug.LogWarning("⚠️ Replace index out of range in move learn callback.");
                }

                decisionMade = true;
            };

            System.Action onCancel = () =>
            {
                Debug.Log($"🕊️ {runtime.baseData.characterName} decided not to learn {newAttack.attackName}.");
                decisionMade = true;
            };

            // If UI manager exists, use it (it will invoke our callbacks when player chooses)
            if (MoveReplaceUIManager.Instance != null)
            {
                var moveNames = new List<string>();
                foreach (var atk in runtime.equippedAttacks)
                    moveNames.Add(atk.attackName);

                MoveReplaceUIManager.Instance.ShowReplacePrompt(moveNames, newAttack.attackName, onReplace, onCancel);

                // wait until player makes a choice (callback flips decisionMade)
                while (!decisionMade)
                    yield return null;
            }
            else
            {
                // Fallback: log instructions and wait for N (skip)
                Debug.LogWarning("⚠️ MoveReplaceUIManager not found. Press [N] to skip learning (one at a time).");
                Debug.Log($"⏸️ {runtime.baseData.characterName} can learn {newAttack.attackName}. Press [N] to skip...");

                // Wait until N pressed
                while (!Input.GetKeyDown(KeyCode.N))
                    yield return null;

                Debug.Log($"🕊️ Skipped learning {newAttack.attackName} for {runtime.baseData.characterName}.");
                // mark as done
                decisionMade = true;
            }

            // small frame yield to ensure UI updates settle
            yield return null;
        }

        // all queued decisions done
        moveLearningInProgress = false;
        queueProcessorCoroutine = null;
        Debug.Log("✅ All move learning complete — gameplay can resume.");
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
