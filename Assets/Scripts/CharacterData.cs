using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "RPG/Character")]
public class CharacterData : ScriptableObject
{   
    // inside CharacterData class (add near other headers)
[Header("Animation Clips (optional)")]
public AnimationClip idleClip;
public AnimationClip attackClip;

/// <summary>
/// Normalized time in attack clip when the "hit" happens (0..1).
/// If <= 0 or >= 1, no mid-hit event is fired. Default 0.5 (middle).
/// </summary>
[Range(0f, 1f)]
public float attackHitNormalized = 0.5f;

    [Header("Basic Info")]
    public string characterName;
    public string characterTag;
    public Sprite portrait;

    [Header("Base Stats")]
    public int baseHP;
    public int baseAttack;
    public int baseDefense;
    public int baseSpeed;

    [Header("Learnable Attacks (Manually Assigned)")]
    public List<LearnableAttack> learnableAttacks = new List<LearnableAttack>();

    [Header("Ultimate Skill (From Weapon)")]
    public AttackData ultimateSkill;

    [Header("XP Reward")]
public int expReward = 50;

    /// <summary>
    /// Returns all attacks the character has unlocked at this level.
    /// </summary>
    public List<AttackData> GetAvailableAttacks(int currentLevel)
    {
        List<AttackData> available = new List<AttackData>();

        foreach (var learnable in learnableAttacks)
        {
            if (currentLevel >= learnable.levelToLearn && learnable.attack != null)
                available.Add(learnable.attack);
        }

        return available;
    }
}

[System.Serializable]
public class LearnableAttack
{
    public AttackData attack;     // Drag & drop your AttackData asset here
    public int levelToLearn = 1;  // The level where this attack unlocks
}
