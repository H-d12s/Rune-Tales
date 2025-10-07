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

    void Start()
    {
        if (characterData != null)
            InitializeCharacter();
    }

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
        originalColor = spriteRenderer.color;

        if (characterData.portrait != null)
            spriteRenderer.sprite = characterData.portrait;

        Debug.Log($"✅ {(isPlayer ? "Player" : "Enemy")} {characterData.characterName} initialized ({runtimeCharacter.currentHP} HP)");
    }

    public CharacterRuntime GetRuntimeCharacter() => runtimeCharacter;

    // === DAMAGE ===
    public void TakeDamage(int amount)
    {
        if (runtimeCharacter == null)
        {
            Debug.LogError($"❌ {characterData.characterName} has no runtime data!");
            return;
        }

        runtimeCharacter.TakeDamage(amount);
        StartCoroutine(FlashOnHit());

        if (runtimeCharacter.currentHP <= 0)
            StartCoroutine(HandleDeath());
    }

    private IEnumerator FlashOnHit()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.15f);
        spriteRenderer.color = originalColor;
    }

    public IEnumerator HandleDeath()
    {
        Debug.Log($"💀 {characterData.characterName} has been defeated!");
        spriteRenderer.color = Color.gray;
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }
}
