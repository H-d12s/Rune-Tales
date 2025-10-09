using UnityEngine;

[CreateAssetMenu(fileName = "NewAttack", menuName = "RPG/Attack")]
public class AttackData : ScriptableObject
{
    [Header("Basic Info")]
    public string attackName;
    public string description;
    public Sprite icon;

    [Header("Stats")]
    public int power;                     // Base damage (multiplied by dice roll)
    public int maxUsage;                  // Like PP
    public int chargeValue;               // Fills ultimate bar
    public bool isAoE = false; // True = AoE attack

    [Header("Dice Roll")]
    public int diceMin = 1;               // Minimum dice roll
    public int diceMax = 10;              // Maximum dice roll

    [Header("Special Effects")]
    public AttackEffectType effectType = AttackEffectType.None;
    [Range(0f, 1f)] public float effectChance = 0f;    // 0.3 = 30%
    public int effectDuration = 0;             
    
    public bool isLifeLeech;
    public float lifeLeechPercent = 0.5f; // heal % of damage dealt         // Turns effect lasts (poison, burn, stun, etc.)

    [Header("Setup / Non-Damage Moves")]
    public bool isNonDamageMove = false; 
    public bool healsTarget = false; // Is this a heal move?


    [Header("Buffs / Debuffs")]
    public bool buffAttack = false;
    public float buffAttackAmount = 0;
    public bool buffDefense = false;
    public float buffDefenseAmount = 0;
    public bool debuffAttack = false;
    public float debuffAttackAmount = 0;
    public bool debuffDefense = false;
    public float debuffDefenseAmount = 0;

   [Header("Speed / Turn Order")]
    public bool buffSpeed = false;
    public float buffSpeedAmount = 0f;       // e.g. +20% speed
    public bool debuffSpeed = false;
    public float debuffSpeedAmount = 0f;     // e.g. -15% speed

          
    public bool usePriority = false;   
    [Header("Conditional / Risk-Reward")]
    public bool scalesWithFallenAllies = false;   // For moves like VENGEANCE
    public float fallenAlliesMultiplier = 1f;     // How much it scales
    
    // For moves like Execution
    public bool scalesWithFallenEnemies = false;
    public float fallenEnemiesMultiplier = 1f; // For moves like Execution

    public bool scalesWithUserHP = false;         // For moves like Purgatory
    public float hpDamageMultiplier = 1f;         // Damage scaling as HP lowers
    public bool conditionalStanceMultiplier = false; // For Ichimonji, Quick Slash, etc.
    public float stanceMultiplier = 1f;

    [Header("Targeting")]
    public bool affectsSelf;
    public bool manualBuffTargetSelection = false;

    [Header("Setup / Next Move Effects")]
    public bool modifiesNextAttack = false;   // True for skills like Assassinate
    public float nextAttackMultiplier = 1f;   // Multiplier applied to next attack
    public bool modifiesNextDice = false;     // True for skills like Locked & Loaded
    public int nextDiceMin = 1;               // Min dice for next attack
    public int nextDiceMax = 10;              // Max dice for next attack

    [HideInInspector] public int currentUsage;

    private void OnEnable()
    {
        currentUsage = maxUsage;
    }
    
    
}

public enum AttackEffectType
{
    None,
    Stun,
    Poison,
    Burn,
    buffAttack,
    buffDefense,
    debuffAttack,   
    debuffDefense
}
