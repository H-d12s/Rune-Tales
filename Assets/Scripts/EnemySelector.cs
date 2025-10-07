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
            flashRoutine = StartCoroutine(FlashRed());
        }
        else
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            spriteRenderer.color = originalColor;
            flashRoutine = null;
        }
    }

    IEnumerator FlashRed()
    {
        while (true)
        {
            // Ping-pong between red and original color
            float t = Mathf.PingPong(Time.time * 4f, 1f); // speed = 4
            spriteRenderer.color = Color.Lerp(originalColor, Color.red, t);
            yield return null;
        }
    }

    public void ResetHighlight()
    {
        Highlight(false);
    }
}
