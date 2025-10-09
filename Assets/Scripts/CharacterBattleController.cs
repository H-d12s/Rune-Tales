using UnityEngine;
using System.Collections;

public class CharacterBattleController : MonoBehaviour
{
    [Header("References")]
    public CharacterData characterData;
    public SpriteRenderer spriteRenderer;

    [HideInInspector] public bool isPlayer;

    private CharacterRuntime runtimeCharacter;
    private Color originalColor;

    // === STANCE SYSTEM ===
    public bool InStance { get; private set; } = false;
    private int stanceTurnsRemaining = 0;

    // === UNITY HOOK ===
    void Start()
    {
        if (characterData != null)
            InitializeCharacter();
    }

    // === INITIALIZATION ===
    public void InitializeCharacter()
    {
        if (characterData == null)
        {
            Debug.LogError($"❌ No CharacterData assigned to {name}!");
            return;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        runtimeCharacter = new CharacterRuntime(characterData, 1);
        runtimeCharacter.controller = this;

        originalColor = spriteRenderer.color;

        if (characterData.portrait != null)
            spriteRenderer.sprite = characterData.portrait;

        Debug.Log($"✅ {(isPlayer ? "Player" : "Enemy")} {characterData.characterName} initialized ({runtimeCharacter.currentHP}/{runtimeCharacter.MaxHP} HP)");
    }

    // === GETTERS ===
    public CharacterRuntime GetRuntimeCharacter() => runtimeCharacter;

    // === STANCE CONTROL ===
    public void EnterStance(int turns)
    {
        InStance = true;
        stanceTurnsRemaining = turns + 1; // +1 so it lasts for the next N turns
        Debug.Log($"🧘‍♂️ {characterData.characterName} entered stance for {turns} turns (will start counting next turn)!");
    }
    private void TickStance()
    {
        if (!InStance) return;

        stanceTurnsRemaining--;
        if (stanceTurnsRemaining <= 0)
        {
            InStance = false;
            Debug.Log($"🌀 {characterData.characterName}'s stance has ended.");
        }
    }

    // === DAMAGE HANDLING ===
    public int TakeDamage(int amount)
    {
        if (runtimeCharacter == null)
        {
            Debug.LogError($"❌ {characterData.characterName} has no runtime data!");
            return 0;
        }

        // Calculate damage taken (in case of overkill)
        int damageTaken = Mathf.Min(amount, runtimeCharacter.currentHP); // Assuming runtimeCharacter has CurrentHP
        runtimeCharacter.TakeDamage(amount); // This will reduce HP
        StartCoroutine(FlashOnHit());

        if (!runtimeCharacter.IsAlive)
            StartCoroutine(HandleDeath());

        return damageTaken;
    }


    private IEnumerator FlashOnHit()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.15f);
        spriteRenderer.color = originalColor;
    }

    // === DEATH HANDLING ===
    public IEnumerator HandleDeath()
    {
        Debug.Log($"💀 {characterData.characterName} has been defeated!");
        if (spriteRenderer != null)
            spriteRenderer.color = Color.gray;

        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    // === TURN-BASED TICK ===
    // Call this exactly once per character per turn from BattleManager
    public void EndTurnEffects()
    {
        // Tick stance first
        TickStance();

        // Tick status effects safely
        runtimeCharacter.TickStatusEffects();

        Debug.Log($"💗 {characterData.characterName} HP: {runtimeCharacter.currentHP}/{runtimeCharacter.MaxHP}");

    }
    

    public bool IsStunned { get; private set; } = false;
private int stunDuration = 0;

public void ApplyStun(int duration)
{
    IsStunned = true;
    stunDuration = duration;
    Debug.Log($"💫 {characterData.characterName} is stunned for {duration} turns!");
}

public bool ShouldSkipTurn()
{
    if (!IsStunned) return false;

    stunDuration--;
    if (stunDuration <= 0)
    {
        IsStunned = false;
        Debug.Log($"✅ {characterData.characterName} is no longer stunned.");
    }

    return true;
}

}
