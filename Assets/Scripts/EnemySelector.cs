using UnityEngine;

[RequireComponent(typeof(CharacterBattleController))]
public class EnemySelector : MonoBehaviour
{
    private CharacterBattleController controller;
    private BattleUIManager uiManager;
    private SpriteRenderer pointerRenderer;

    void Start()
    {
        controller = GetComponent<CharacterBattleController>();
        uiManager = FindFirstObjectByType<BattleUIManager>();

        if (uiManager == null)
        {
            Debug.LogError("❌ BattleUIManager not found in scene!");
            return;
        }

        // Find the pointer sprite (child named "Pointer")
        pointerRenderer = transform.Find("Pointer")?.GetComponent<SpriteRenderer>();
        if (pointerRenderer != null)
            pointerRenderer.enabled = false; // Hide by default
    }

    void OnMouseDown()
    {
        if (uiManager == null || controller == null)
            return;

        uiManager.SetTarget(controller);
    }

    // Called by UI manager to visually highlight
    public void Highlight(bool active)
    {
        if (pointerRenderer != null)
            pointerRenderer.enabled = active;
    }
}
