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

    private static EnemySelector currentlySelected; // ✅ only one can be active

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
            Debug.LogError("❌ EnemySelector: BattleUIManager not found in scene!");
    }

    void OnMouseDown()
    {
        if (uiManager == null || controller == null)
            return;

        if (!uiManager.IsSelectingTarget())
        {
            Debug.Log("⛔ Not in targeting mode — ignoring click");
            return;
        }

        // Deselect previously selected enemy
        if (currentlySelected != null && currentlySelected != this)
            currentlySelected.Highlight(false);

        // Select this one
        currentlySelected = this;
        uiManager.SetTarget(controller);
        Highlight(true);
    }

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

            // Clear global selection if this was selected
            if (currentlySelected == this)
                currentlySelected = null;
        }
    }

    IEnumerator SmoothFlashRed()
    {
        while (true)
        {
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) / 2f;
            spriteRenderer.color = Color.Lerp(originalColor, Color.red, pulse);
            yield return null;
        }
    }

    public void ResetHighlight()
    {
        Highlight(false);
    }

    public void DisableSelection()
    {
        // ❌ Stop flashing, disable click
        Highlight(false);
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;
    }
}
