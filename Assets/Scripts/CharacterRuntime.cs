using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterRuntime
{
    public CharacterData baseData;
    public int currentLevel;

    // --- Dynamic Stats ---
    public int currentHP;
    public List<AttackData> equippedAttacks = new List<AttackData>();

    // --- Stat Accessors ---
    public int MaxHP => baseData != null ? baseData.baseHP : 0;
    public int Attack => baseData != null ? baseData.baseAttack : 0;
    public int Defense => baseData != null ? baseData.baseDefense : 0;
    public int Speed => baseData != null ? baseData.baseSpeed : 0;

    // --- Constructor ---
    public CharacterRuntime(CharacterData data, int level)
    {
        if (data == null)
        {
            Debug.LogError("❌ CharacterRuntime initialized with null CharacterData!");
            return;
        }

        baseData = data;
        currentLevel = level;
        currentHP = MaxHP;

        // Initialize attacks
        if (data.learnableAttacks != null)
        {
            foreach (var entry in data.learnableAttacks)
            {
                if (entry?.attack != null && entry.levelToLearn <= level)
                    equippedAttacks.Add(entry.attack);
            }
        }

        // Add ultimate if exists
        if (data.ultimateSkill != null && !equippedAttacks.Contains(data.ultimateSkill))
            equippedAttacks.Add(data.ultimateSkill);

        // Limit to 2 attacks for simplicity
        if (equippedAttacks.Count > 2)
            equippedAttacks = equippedAttacks.GetRange(0, 2);

        Debug.Log($"✅ Created runtime for {data.characterName} (Lvl {level}) with {equippedAttacks.Count} attacks.");
    }

    // --- Damage / Heal ---
    public void TakeDamage(int damage)
    {
        damage = Mathf.Max(1, damage);
        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"💔 {baseData.characterName} took {damage} damage! (HP: {currentHP}/{MaxHP})");

        if (currentHP <= 0)
            Debug.Log($"☠️ {baseData.characterName} has been defeated!");
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHP = Mathf.Min(MaxHP, currentHP + amount);
        Debug.Log($"💚 {baseData.characterName} healed {amount} HP! (HP: {currentHP}/{MaxHP})");
    }

    // --- Helper ---
    public bool IsAlive => currentHP > 0;
}
