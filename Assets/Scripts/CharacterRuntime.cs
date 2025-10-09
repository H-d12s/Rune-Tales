using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterRuntime
{
    public CharacterData baseData;
    public int currentLevel;

    // --- Dynamic Stats ---
    public int currentHP;
    public int currentXP;
    public int xpToNextLevel;

    // --- Runtime copies of stats (modifiable at runtime) ---
    public int runtimeHP;
    public int runtimeAttack;
    public int runtimeDefense;
    public int runtimeSpeed;

    // --- Buffs/Debuffs ---
    private int attackModifier = 0;
    private int defenseModifier = 0;
    private int speedModifier = 0;

    // --- Setup Move Tracking ---
    public int nextDiceMin = 0;
    public int nextDiceMax = 0;
    public float nextAttackMultiplier = 1f;

    // --- Equipped Attacks ---
    public List<AttackData> equippedAttacks = new List<AttackData>();

    // --- Stat Accessors ---
    public int MaxHP => runtimeHP;
    public int Attack => Mathf.Max(1, runtimeAttack + attackModifier);
    public int Defense => Mathf.Max(1, runtimeDefense + defenseModifier);
    public int Speed => Mathf.Max(1, runtimeSpeed + speedModifier);

    public StatusEffectManager statusEffectManager;
    public CharacterBattleController controller;

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

        runtimeHP = data.baseHP;
        runtimeAttack = data.baseAttack;
        runtimeDefense = data.baseDefense;
        runtimeSpeed = data.baseSpeed;

        currentHP = MaxHP;

        statusEffectManager = new StatusEffectManager(this);

        // Initialize attacks
        if (data.learnableAttacks != null)
        {
            foreach (var entry in data.learnableAttacks)
            {
                if (entry?.attack != null && entry.levelToLearn <= level)
                    equippedAttacks.Add(entry.attack);
            }
        }

        if (data.ultimateSkill != null && !equippedAttacks.Contains(data.ultimateSkill))
            equippedAttacks.Add(data.ultimateSkill);

        if (equippedAttacks.Count > 2)
            equippedAttacks = equippedAttacks.GetRange(0, 2);

        Debug.Log($"✅ Created runtime for {data.characterName} (Lvl {level}) with {equippedAttacks.Count} attacks.");
    }

    // --- Damage / Heal ---
    public void TakeDamage(int damage)
    {
        damage = Mathf.Max(1, damage);
        currentHP = Mathf.Max(0, currentHP - damage);

        if (currentHP <= 0)
            Debug.Log($"☠️ {baseData.characterName} has been defeated!");
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;

        int healedAmount = Mathf.Min(amount, MaxHP - currentHP);
        currentHP += healedAmount;
        Debug.Log($"💚 {baseData.characterName} healed for {healedAmount} HP ({currentHP}/{MaxHP})");
    }

    // --- Buff / Debuff (percent-based) ---
    public void ModifyAttack(float percent)
    {
        int oldAttack = Attack;
        int change = Mathf.RoundToInt(runtimeAttack * percent);
        attackModifier += change;
        Debug.Log($"{baseData.characterName}'s Attack changed by {percent * 100}%: {oldAttack} → {Attack}");
    }

    public void ModifyDefense(float percent)
    {
        int oldDefense = Defense;
        int change = Mathf.RoundToInt(runtimeDefense * percent);
        defenseModifier += change;
        Debug.Log($"{baseData.characterName}'s Defense changed by {percent * 100}%: {oldDefense} → {Defense}");
    }

    public void ModifySpeed(float percent)
    {
        int oldSpeed = Speed;
        int change = Mathf.RoundToInt(runtimeSpeed * percent);
        speedModifier += change;
        Debug.Log($"{baseData.characterName}'s Speed changed by {percent * 100}%: {oldSpeed} → {Speed}");
    }

    // --- Apply Buffs/Debuffs from AttackData ---
    public void ApplyBuffsAndDebuffs(AttackData attack, bool selfCast = false)
    {
        if (attack == null) return;

        // Buffs
        if (attack.buffAttack) ModifyAttack(attack.buffAttackAmount);
        if (attack.buffDefense) ModifyDefense(attack.buffDefenseAmount);
        if (attack.buffSpeed) ModifySpeed(attack.buffSpeedAmount);

        // Debuffs
        if (attack.debuffAttack) ModifyAttack(-Mathf.Abs(attack.debuffAttackAmount));
        if (attack.debuffDefense) ModifyDefense(-Mathf.Abs(attack.debuffDefenseAmount));
        if (attack.debuffSpeed) ModifySpeed(-Mathf.Abs(attack.debuffSpeedAmount));

        Debug.Log($"✨ {baseData.characterName} {(selfCast ? "used a self buff" : "was affected")} from {attack.attackName}.");
    }

    // --- Setup Move Helpers ---
    public void SetNextDiceRange(int min, int max)
    {
        nextDiceMin = min;
        nextDiceMax = max;
    }

    public void ResetNextDiceRange()
    {
    nextDiceMin = 0;
    nextDiceMax = 0;
    }

    public void SetNextAttackMultiplier(float multiplier)
    {
        nextAttackMultiplier = multiplier;
    }

    public (int, int) ConsumeDiceRange()
    {
        var result = (nextDiceMin, nextDiceMax);
        nextDiceMin = 0;
        nextDiceMax = 0;
        return result;
    }

    public float ConsumeAttackMultiplier()
    {
        float result = nextAttackMultiplier;
        nextAttackMultiplier = 1f;
        return result;
    }

    // --- Status Effects ---
    public void ApplyStatusEffect(AttackEffectType effect, int duration)
    {
        if (effect == AttackEffectType.None) return;

        switch (effect)
        {
            case AttackEffectType.Burn:
                statusEffectManager.ApplyEffect(StatusEffectType.Burn, duration);
                break;
            case AttackEffectType.Poison:
                statusEffectManager.ApplyEffect(StatusEffectType.Poison, duration);
                break;
            case AttackEffectType.Stun:
                controller.ApplyStun(duration);
                break;
        }
    }

    public void TickStatusEffects()
    {
        if (statusEffectManager != null && controller != null)
            statusEffectManager.TickEffects(controller);
    }

    // --- Helper ---
    public bool IsAlive => currentHP > 0;
}
