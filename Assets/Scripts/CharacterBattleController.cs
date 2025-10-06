using UnityEngine;

public class CharacterBattleController : MonoBehaviour
{
    [Header("References")]
    public CharacterData characterData;
    public SpriteRenderer spriteRenderer;

    private CharacterRuntime runtimeCharacter;

    void Start()
    {
        // Only auto-initialize if CharacterData was assigned before Start()
        if (characterData != null)
            InitializeCharacter();
    }

    /// <summary>
    /// Called by BattleManager right after instantiation to set up the character.
    /// </summary>
    public void InitializeCharacter()
    {
        if (characterData == null)
        {
            Debug.LogError($"❌ No CharacterData assigned to {name}!");
            return;
        }

        // Ensure we have a SpriteRenderer
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // Create runtime data
        runtimeCharacter = new CharacterRuntime(characterData, 1); // Level 1 start

        // Assign sprite from CharacterData
        if (characterData.portrait != null)
        {
            spriteRenderer.sprite = characterData.portrait;
            Debug.Log($"🖼️ Assigned sprite for {characterData.characterName}");
        }
        else
        {
            Debug.LogWarning($"⚠️ {characterData.characterName} has no portrait assigned!");
        }

        Debug.Log($"✅ Spawned {characterData.characterName} with {runtimeCharacter.equippedAttacks.Count} attacks.");
    }

    /// <summary>
    /// Expose runtime character for other scripts (like UI or BattleManager).
    /// </summary>
    public CharacterRuntime GetRuntimeCharacter()
    {
        if (runtimeCharacter == null)
        {
            Debug.LogWarning($"⚠️ Runtime character for {characterData?.characterName ?? "UNKNOWN"} not yet initialized!");
        }
        return runtimeCharacter;
    }
}
