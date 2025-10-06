using UnityEngine;

[CreateAssetMenu(fileName = "NewAttack", menuName = "RPG/Attack")]
public class AttackData : ScriptableObject
{
    [Header("Basic Info")]
    public string attackName;
    public string description;
    public Sprite icon;

    [Header("Stats")]
    public int power;            // Base damage
    public int maxUsage;         // Like PP
    public int chargeValue;      // Fills the ultimate bar
    public int priority = 0;     // Higher = acts earlier in the turn
    public bool targetsAllEnemies = false; // True = hits all enemies

    [Header("Special Effect")]
    public AttackEffectType effectType = AttackEffectType.None;
    [Range(0f, 1f)] public float effectChance = 0f; // 0.3 = 30% chance
    public int effectDuration = 0;                  // Turns effect lasts (for poison/stun/etc.)

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
    Heal,
    BuffAttack,
    BuffDefense,
    DebuffAttack,
    DebuffDefense
}
