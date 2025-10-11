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

    // Rebirth tracking (Pyromancer / Roy)
    public bool hasRevivedOnce = false;

    // --- Runtime copies of stats (modifiable at runtime) ---
    public int runtimeHP;
    public int runtimeAttack;
    public int runtimeDefense;
    public int runtimeSpeed;

    // --- Buffs/Debuffs (flat integer modifiers applied on top of runtime stats) ---
    private int attackModifier = 0;
    private int defenseModifier = 0;
    private int speedModifier = 0;

    // --- Setup Move Tracking ---
    public int nextDiceMin = 0;
    public int nextDiceMax = 0;
    public float nextAttackMultiplier = 1f;

    // --- Equipped Attacks ---
    public List<AttackData> equippedAttacks = new List<AttackData>();

    // --- Status & Controller refs ---
    public StatusEffectManager statusEffectManager;
    public CharacterBattleController controller;

    // --- Assassinate mark fields ---
    public CharacterRuntime markedBy = null;
    public float markDamageMultiplier = 1f;
    public int markDuration = 0;

    // --- Convenience alias (some older code used 'characterData') ---
    public CharacterData characterData => baseData;

    // --- Stat Accessors (include modifiers) ---
    public int MaxHP => runtimeHP;
    public int Attack => Mathf.Max(1, runtimeAttack + attackModifier);
    public int Defense => Mathf.Max(1, runtimeDefense + defenseModifier);
    public int Speed => Mathf.Max(1, runtimeSpeed + speedModifier);

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

        // Copy base stats
        runtimeHP = data.baseHP;
        runtimeAttack = data.baseAttack;
        runtimeDefense = data.baseDefense;
        runtimeSpeed = data.baseSpeed;

        currentHP = MaxHP;

        // Status system
        statusEffectManager = new StatusEffectManager(this);

        // Initialize attacks / learned moves
        if (data.learnableAttacks != null)
        {
            foreach (var entry in data.learnableAttacks)
            {
                if (entry?.attack != null && entry.levelToLearn <= level)
                    equippedAttacks.Add(entry.attack);
            }
        }

        // Add ultimate skill if present and not already present
        if (data.ultimateSkill != null && !equippedAttacks.Contains(data.ultimateSkill))
            equippedAttacks.Add(data.ultimateSkill);

        // Keep only first 2 attacks (existing behavior)
        if (equippedAttacks.Count > 2)
            equippedAttacks = equippedAttacks.GetRange(0, 2);

        Debug.Log($"✅ Created runtime for {data.characterName} (Lvl {level}) with {equippedAttacks.Count} attacks.");
    }

    // --- Damage / Heal ---
    /// <summary>
    /// Apply damage to this character. Returns the damage value applied (after marks/rebirth logic).
    /// Optional attacker parameter is used for Assassinate-mark logic.
    /// </summary>
    public int TakeDamage(int damage, CharacterRuntime attacker = null)
    {
        // --- Assassinate mark bonus ---
        if (attacker != null && markedBy == attacker && markDuration > 0)
        {
            Debug.Log($"💀 {baseData.characterName} is marked by {attacker.baseData.characterName}! Taking {markDamageMultiplier}x damage.");
            damage = Mathf.RoundToInt(damage * markDamageMultiplier);

            // Consume mark (mark applies once)
            markDuration--;
            if (markDuration <= 0)
            {
                markedBy = null;
                markDamageMultiplier = 1f;
            }
        }

        // --- Normal damage application ---
        damage = Mathf.Max(1, damage);
        int prevHP = currentHP;
        currentHP = Mathf.Max(0, currentHP - damage);
        int applied = prevHP - currentHP;

        // --- Rebirth passive example (Pyromancer "Roy") ---
        if (currentHP <= 0)
        {
            if (baseData != null && baseData.characterName == "Roy" && !hasRevivedOnce)
            {
                hasRevivedOnce = true;

                // Revive with 40% HP
                int reviveHP = Mathf.RoundToInt(MaxHP * 0.4f);
                currentHP = reviveHP;

                // Increase Attack by 20% of base attack (applies to runtimeAttack)
                int attackBoost = Mathf.RoundToInt(baseData.baseAttack * 0.2f);
                runtimeAttack += attackBoost;

                Debug.Log($"🔥 {baseData.characterName} resurrects in flames with {reviveHP} HP and +20% Attack!");
                // We consider damage as 'applied' (it was), so return applied
                return applied;
            }

            // Normal death (no rebirth available)
            Debug.Log($"☠️ {baseData.characterName} has been defeated!");
        }

        return applied;
    }

    /// <summary>
    /// Backwards-compatible call used by older code that passed only an amount.
    /// </summary>
    public int TakeDamage(int damage)
    {
        return TakeDamage(damage, null);
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;

        if (!IsAlive)
        {
            // If dead, do nothing (unless you want to allow revive items via Heal)
            return;
        }

        int healedAmount = Mathf.Min(amount, MaxHP - currentHP);
        currentHP += healedAmount;
        Debug.Log($"💚 {baseData.characterName} healed for {healedAmount} HP ({currentHP}/{MaxHP})");
    }

    // --- Buff / Debuff (percent-based) ---
    // These take a percent (e.g. 0.2 for +20%) and convert to flat integer changes against runtime base stat.
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

        // Debuffs (ensure negative percentages)
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
                if (controller != null)
                    controller.ApplyStun(duration);
                break;
        }
    }

    public void TickStatusEffects()
    {
        if (statusEffectManager != null && controller != null)
            statusEffectManager.TickEffects(controller);
    }

    // --- Assassinate / Mark helpers ---
    public void ApplyTemporaryMark(CharacterRuntime attacker, float multiplier, int duration)
    {
        markedBy = attacker;
        markDamageMultiplier = multiplier;
        markDuration = duration;
        Debug.Log($"🔪 {baseData.characterName} is marked for assassination by {attacker.baseData.characterName} for {duration} turn(s)!");
    }

    /// <summary>
    /// Consume the mark (if applicable) and return the multiplier that should be applied.
    /// </summary>
    public float ConsumeMarkIfApplicable(CharacterRuntime attacker)
    {
        if (markedBy == attacker && markDuration > 0)
        {
            markDuration--;
            if (markDuration <= 0)
            {
                markedBy = null;
                float multiplier = markDamageMultiplier;
                markDamageMultiplier = 1f;
                return multiplier;
            }
            return markDamageMultiplier;
        }
        return 1f; // no extra damage
    }

    public void DecrementMarkDuration()
    {
        if (markDuration > 0)
        {
            markDuration--;
            if (markDuration <= 0)
            {
                markedBy = null;
                markDamageMultiplier = 1f;
            }
        }
    }

    // --- Helper ---
    public bool IsAlive => currentHP > 0;
}
