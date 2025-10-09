using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterBattleController))]
public class TargetSelector : MonoBehaviour
{
    private CharacterBattleController controller;
    private BattleUIManager uiManager;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalPosition;
    private Coroutine flashRoutine;

    private static TargetSelector currentlySelected; // Only one highlighted at a time

    public TargetType targetType = TargetType.Enemy; // Will be set dynamically

    void Awake()
    {
        controller = GetComponent<CharacterBattleController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalPosition = transform.localPosition;

        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    void Start()
    {
        uiManager = FindFirstObjectByType<BattleUIManager>();
        if (uiManager == null)
            Debug.LogError("❌ TargetSelector: BattleUIManager not found in scene!");
    }

    void OnMouseDown()
    {
        if (uiManager == null || controller == null) return;

        if (!uiManager.IsSelectingTarget()) return;

        // Dynamically set targetType based on character
        targetType = controller.isPlayer ? TargetType.Ally : TargetType.Enemy;

        if (!uiManager.CanSelectTargetType(targetType)) return;

        if (currentlySelected != null && currentlySelected != this)
            currentlySelected.Highlight(false);

        currentlySelected = this;

        Highlight(true);
        uiManager.SetTarget(controller);
    }

    public void Highlight(bool on)
    {
        if (spriteRenderer == null) return;

        if (on)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(SmoothFlash());
        }
        else
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = null;
            spriteRenderer.color = originalColor;
            transform.localPosition = originalPosition;
            if (currentlySelected == this) currentlySelected = null;
        }
    }

    private IEnumerator SmoothFlash()
    {
        float flashTime = 0f;
        Color targetColor = targetType == TargetType.Enemy
            ? new Color(1f, 0.3f, 0.3f)  // softer red
            : new Color(0.3f, 1f, 0.3f); // green for allies

        while (true)
        {
            flashTime += Time.deltaTime * 4f;
            float pulse = (Mathf.Sin(flashTime) + 1f) * 0.25f; // lower intensity
            spriteRenderer.color = Color.Lerp(originalColor, targetColor, pulse);

            // horizontal shake
            float shakeAmount = 0.05f;
            transform.localPosition = originalPosition + new Vector3(Mathf.Sin(flashTime * 2f) * shakeAmount, 0, 0);

            yield return null;
        }
    }

    public void ResetHighlight()
    {
        Highlight(false);
    }

    public void DisableSelection()
    {
        Highlight(false);
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }
}
