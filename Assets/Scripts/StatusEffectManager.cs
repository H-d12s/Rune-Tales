using System.Collections.Generic;
using UnityEngine;

public enum StatusEffectType
{
    None,
    Burn,
    Poison,
    Stun
}

public class StatusEffectManager
{
    private CharacterRuntime target;
    private Dictionary<StatusEffectType, StatusEffectData> activeEffects = new Dictionary<StatusEffectType, StatusEffectData>();

    public StatusEffectManager(CharacterRuntime targetCharacter)
    {
        target = targetCharacter;
    }

    public void ApplyEffect(StatusEffectType effect, int duration)
    {
        if (effect == StatusEffectType.None || target == null) return;

        // Refresh duration if effect already active
        if (activeEffects.ContainsKey(effect))
        {
            activeEffects[effect].duration = duration; 
            return;
        }

        StatusEffectData data = new StatusEffectData(duration + 1, effect);

        // Apply stat changes using the predefined methods
        switch (effect)
        {
            case StatusEffectType.Burn:
                data.attackModifier = 0f;
                data.defenseModifier = -0.1f; // reduce defense by 10%
                target.ModifyDefense(data.defenseModifier);
                break;
            case StatusEffectType.Poison:
                data.attackModifier = -0.1f; // reduce attack by 10%
                data.defenseModifier = 0f;
                target.ModifyAttack(data.attackModifier);
                break;
            case StatusEffectType.Stun:
                // Stun handled in CharacterBattleController
                break;
        }

        activeEffects.Add(effect, data);
        Debug.Log($"{target.baseData.characterName} is now affected by {effect} for {duration} turns!");
    }

    public void TickEffects(CharacterBattleController controller)
    {
        if (target == null || !target.IsAlive) return;

        List<StatusEffectType> expired = new List<StatusEffectType>();

        foreach (var kvp in activeEffects)
        {
            StatusEffectType effect = kvp.Key;
            StatusEffectData data = kvp.Value;

            if (data.duration < 1) continue; // skip first turn if needed

            switch (effect)
            {
                case StatusEffectType.Burn:
                case StatusEffectType.Poison:
                    ApplyDamageOverTime(effect, 0.05f);
                    break;
                case StatusEffectType.Stun:
                    // Duration handled in CharacterBattleController
                    break;
            }

            data.duration--;
            if (data.duration <= 0)
                expired.Add(effect);
        }

        // Remove expired effects and revert stat changes
        foreach (var e in expired)
        {
            var data = activeEffects[e];

            if (data.attackModifier != 0f)
                target.ModifyAttack(-data.attackModifier); // revert attack
            if (data.defenseModifier != 0f)
                target.ModifyDefense(-data.defenseModifier); // revert defense

            activeEffects.Remove(e);
            Debug.Log($"✅ {target.baseData.characterName} is no longer affected by {e}");
        }
    }

    private void ApplyDamageOverTime(StatusEffectType type, float percent)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(target.MaxHP * percent));
        target.TakeDamage(damage);
        Debug.Log($"{type} deals {damage} damage to {target.baseData.characterName}");
    }

    public bool HasEffect(StatusEffectType effect) => activeEffects.ContainsKey(effect);
}

public class StatusEffectData
{
    public int duration;
    public StatusEffectType effectType;
    public float attackModifier;
    public float defenseModifier;

    public StatusEffectData(int duration, StatusEffectType type)
    {
        this.duration = duration;
        effectType = type;
        attackModifier = 0f;
        defenseModifier = 0f;
    }
}
