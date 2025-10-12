using UnityEngine;

/// <summary>
/// Scales enemy stats based on encounter number.
/// Boss stats are excluded from scaling.
/// </summary>
public static class EnemyStatScaler
{
    // Adjust these to control growth rate
    private const float BASE_MULTIPLIER = 1.0f;
    private const float GROWTH_PER_ENCOUNTER = 0.05f; // +5% per encounter
    private const float MAX_MULTIPLIER = 3.0f;        // cap at 300% (so they don’t explode in power)

    /// <summary>
    /// Returns scaled copies of the given enemy data for this encounter.
    /// </summary>
    public static CharacterData GetScaledEnemy(CharacterData original, int encounterNumber)
    {
        // Safety check
        if (original == null)
            return null;

        // Skip scaling if this is a boss encounter (10, 20, 30, etc.)
        if (encounterNumber % 10 == 0)
            return original;

        // Create a temporary instance (to not modify the original ScriptableObject)
        CharacterData scaledEnemy = ScriptableObject.CreateInstance<CharacterData>();
        scaledEnemy.characterName = original.characterName + " (Lv." + encounterNumber + ")";
        scaledEnemy.portrait = original.portrait;

        // Copy other fixed data (attacks, etc.)
        scaledEnemy.learnableAttacks = original.learnableAttacks;
        scaledEnemy.baseSpeed = original.baseSpeed; // Optional: speed can stay flat

        // Compute scaling factor
        float multiplier = Mathf.Min(BASE_MULTIPLIER + (encounterNumber - 1) * GROWTH_PER_ENCOUNTER, MAX_MULTIPLIER);

        // Apply scaling
        scaledEnemy.baseHP = Mathf.RoundToInt(original.baseHP * multiplier);
        scaledEnemy.baseAttack = Mathf.RoundToInt(original.baseAttack * multiplier);
        scaledEnemy.baseDefense = Mathf.RoundToInt(original.baseDefense * multiplier);

        return scaledEnemy;
    }
}
