using UnityEngine;
using System.Collections.Generic;

public class ExperienceSystem : MonoBehaviour
{
    [Header("XP Curve Settings")]
    public int baseXPRequired = 50;
    public float growthRate = 1.2f; 
    public int maxLevel = 30;

    [Header("Debug Info")]
    public int totalXPThisBattle;

    private List<CharacterBattleController> playerControllers;

    public void Initialize(List<CharacterBattleController> playerTeam)
    {
        playerControllers = playerTeam;
        totalXPThisBattle = 0;
    }

    /// <summary>
    /// Called from BattleManager when an enemy dies.
    /// </summary>
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

            // Stat scaling
            ApplyStatGrowth(runtime);

            // Unlock new attacks
            LearnNewAttacks(runtime);

            Debug.Log($"⬆️ {runtime.baseData.characterName} leveled up! (Now Level {runtime.currentLevel})");
            ShowLevelUpPopup(runtime.baseData.characterName, runtime.currentLevel);
        }

        PlayerXPData[runtime.baseData.characterName] = currentXP;
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
            default:
                break;
        }

        data.baseHP = Mathf.RoundToInt(data.baseHP * (1 + hpGrowth));
        data.baseAttack = Mathf.RoundToInt(data.baseAttack * (1 + atkGrowth));
        data.baseDefense = Mathf.RoundToInt(data.baseDefense * (1 + defGrowth));
        data.baseSpeed = Mathf.RoundToInt(data.baseSpeed * (1 + spdGrowth));

        runtime.currentHP = runtime.MaxHP;

        Debug.Log($"📈 {data.characterName} stats increased!");
        Debug.Log($"HP: {data.baseHP}, ATK: {data.baseAttack}, DEF: {data.baseDefense}, SPD: {data.baseSpeed}");
    }

    private void LearnNewAttacks(CharacterRuntime runtime)
    {
        var newAttacks = runtime.baseData.GetAvailableAttacks(runtime.currentLevel);
        foreach (var attack in newAttacks)
        {
            if (!runtime.equippedAttacks.Contains(attack))
            {
                runtime.equippedAttacks.Add(attack);
                Debug.Log($"✨ {runtime.baseData.characterName} learned {attack.attackName}!");
                ShowLearnAttackPopup(runtime.baseData.characterName, attack.attackName);
            }
        }
    }

    private void ShowLevelUpPopup(string charName, int newLevel)
    {
        Debug.Log($"🎉 {charName} reached Level {newLevel}!");
        // OPTIONAL: hook to a UI popup later
    }

    private void ShowLearnAttackPopup(string charName, string attackName)
    {
        Debug.Log($"🔥 {charName} learned a new attack: {attackName}!");
        // OPTIONAL: hook to popup UI later
    }

    public int GetXPToNextLevel(int currentLevel)
    {
        return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(growthRate, currentLevel - 1));
    }

    private static Dictionary<string, int> PlayerXPData = new Dictionary<string, int>();
}
//hi