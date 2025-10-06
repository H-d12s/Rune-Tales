using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterRuntime
{
    public CharacterData baseData;
    public int currentLevel;

    // Dynamic battle stats
    public int currentHP;
    public List<AttackData> equippedAttacks = new List<AttackData>();

    public CharacterRuntime(CharacterData data, int level)
    {
        baseData = data;
        currentLevel = level;

        // Defensive checks
        if (data == null)
        {
            Debug.LogError("CharacterRuntime initialized with null CharacterData!");
            return;
        }

        // Set HP
        currentHP = data.baseHP;

        // Initialize attacks list safely
        if (data.learnableAttacks != null)
        {
            foreach (var entry in data.learnableAttacks)
            {
                if (entry != null && entry.attack != null && entry.levelToLearn <= level)
                {
                    equippedAttacks.Add(entry.attack);
                }
            }
        }

        // Add ultimate skill (optional)
        if (data.ultimateSkill != null && !equippedAttacks.Contains(data.ultimateSkill))
        {
            equippedAttacks.Add(data.ultimateSkill);
        }

        // Limit to 2 attacks max (like Pokémon)
        if (equippedAttacks.Count > 2)
            equippedAttacks = equippedAttacks.GetRange(0, 2);
    }

    public void TakeDamage(int damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(baseData.baseHP, currentHP + amount);
    }
}
