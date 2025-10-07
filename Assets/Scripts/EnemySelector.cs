using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterBattleController))]
public class EnemySelector : MonoBehaviour
{
    private CharacterBattleController controller;
    private BattleUIManager uiManager;
    private SpriteRenderer spriteRenderer;

    private Color originalColor;
    private Coroutine flashRoutine;

    private static EnemySelector currentlySelected; // Only one highlighted at a time

    void Awake()
    {
        controller = GetComponent<CharacterBattleController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void Start()
    {
        uiManager = FindFirstObjectByType<BattleUIManager>();
        if (uiManager == null)
        {
            Debug.LogError("❌ EnemySelector: BattleUIManager not found in scene!");
        }
    }

    void OnMouseDown()
    {
        if (uiManager == null || controller == null)
            return;

        // 🔸 Only allow selecting when in targeting mode
        if (!uiManager.IsSelectingTarget())
            return;

        // Deselect previous enemy
        if (currentlySelected != null && currentlySelected != this)
            currentlySelected.Highlight(false);

        currentlySelected = this;

        // Highlight red briefly to confirm click
        Highlight(true);

        // Send selection to BattleUIManager
        uiManager.SetTarget(controller);
    }

    /// <summary>
    /// Enables or disables red highlight flashing.
    /// </summary>
    public void Highlight(bool on)
    {
        if (spriteRenderer == null)
            return;

        if (on)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(SmoothFlashRed());
        }
        else
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = null;
            spriteRenderer.color = originalColor;

            if (currentlySelected == this)
                currentlySelected = null;
        }
    }

    /// <summary>
    /// Smooth flashing red effect for selection.
    /// </summary>
    private IEnumerator SmoothFlashRed()
    {
        float flashTime = 0f;
        while (true)
        {
            flashTime += Time.deltaTime * 4f;
            float pulse = (Mathf.Sin(flashTime) + 1f) * 0.5f;
            spriteRenderer.color = Color.Lerp(originalColor, Color.red, pulse);
            yield return null;
        }
    }

    /// <summary>
    /// Immediately stops any highlight and resets color.
    /// </summary>
    public void ResetHighlight()
    {
        Highlight(false);
    }

    /// <summary>
    /// Used when the enemy dies — disables its collider and highlight completely.
    /// </summary>
    public void DisableSelection()
    {
        Highlight(false);
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
    }
}
