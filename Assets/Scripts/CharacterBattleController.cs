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

    // === STUN SYSTEM ===
    public bool IsStunned { get; private set; } = false;
    private int stunDuration = 0;

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

        // Only create a new runtime if none exists (supports persistence)
        if (runtimeCharacter == null)
        {
            runtimeCharacter = new CharacterRuntime(characterData, 1);
        }
        else
        {
            Debug.Log($"♻️ Reusing existing runtime for {characterData.characterName}");
        }

        // ensure controller link
        runtimeCharacter.controller = this;

        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (characterData.portrait != null && spriteRenderer != null)
            spriteRenderer.sprite = characterData.portrait;

        // If saved runtime was empty HP, reset to maxHP
        if (runtimeCharacter.currentHP <= 0)
            runtimeCharacter.currentHP = runtimeCharacter.runtimeHP;

        Debug.Log($"✅ {(isPlayer ? "Player" : "Enemy")} {characterData.characterName} initialized ({runtimeCharacter.currentHP}/{runtimeCharacter.runtimeHP} HP)");
    }

    // === GETTER ===
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

    // === DAMAGE HANDLING (overloads) ===
    /// <summary>
    /// Old-style call: apply damage without specifying attacker.
    /// Returns the amount of HP actually removed.
    /// </summary>
    public int TakeDamage(int amount)
    {
        return TakeDamage(amount, null);
    }

    /// <summary>
    /// Preferred: pass attacker runtime so marks (Assassinate) can be applied.
    /// Returns the amount of HP actually removed.
    /// </summary>
    public int TakeDamage(int amount, CharacterRuntime attacker)
    {
        if (runtimeCharacter == null)
        {
            Debug.LogError($"❌ {characterData?.characterName ?? name} has no runtime data!");
            return 0;
        }

        // Ask runtime to apply damage (it handles marks, rebirth, etc.)
        int applied = runtimeCharacter.TakeDamage(amount, attacker);

        // Visual feedback
        StartCoroutine(FlashOnHit());

        // If runtime now not alive, run death flow (note: runtime may have resurrected itself)
        if (!runtimeCharacter.IsAlive)
            StartCoroutine(HandleDeath());

        return applied;
    }

    private IEnumerator FlashOnHit()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.15f);
        // Protect against spriteRenderer having been destroyed
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
    }

    // === DEATH HANDLING ===
    public IEnumerator HandleDeath()
    {
        Debug.Log($"💀 {characterData.characterName} has been defeated!");
        if (spriteRenderer != null)
            spriteRenderer.color = Color.gray;

        yield return new WaitForSeconds(0.5f);

        // Destroy the GameObject (BattleManager handles removing references / xp etc.)
        if (gameObject != null)
            Destroy(gameObject);
    }

    // === TURN-BASED TICK ===
    // Call this exactly once per character per turn from BattleManager
    public void EndTurnEffects()
    {
        // Tick stance first
        TickStance();

        // Tick status effects safely
        if (runtimeCharacter != null)
            runtimeCharacter.TickStatusEffects();

        if (characterData != null && runtimeCharacter != null)
            Debug.Log($"💗 {characterData.characterName} HP: {runtimeCharacter.currentHP}/{runtimeCharacter.MaxHP}");
    }

    // === STUN ===
    public void ApplyStun(int duration)
    {
        IsStunned = true;
        stunDuration = duration;
        Debug.Log($"💫 {characterData.characterName} is stunned for {duration} turns!");
    }

    /// <summary>
    /// Returns true if the character should skip their turn (i.e., they are stunned this turn).
    /// This will decrement the stun duration.
    /// </summary>
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
