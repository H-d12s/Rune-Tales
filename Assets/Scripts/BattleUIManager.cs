using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
//hi
public class BattleUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainActionPanel;     // Panel with Attack / Equipment / Persuade / Retreat
    public GameObject attackSelectionPanel; // Panel with actual attack buttons

    [Header("Buttons")]
    public Button attackButton;
    public Button equipmentButton;
    public Button persuadeButton;
    public Button retreatButton;

    [Header("Attack Buttons")]
    public List<Button> attackButtons; // Assign 2 buttons here for now (can add more later)

    [Header("References")]
    public CharacterBattleController playerController;

    private CharacterRuntime playerRuntime;
    private CharacterBattleController currentTarget; // ✅ Selected enemy target

    void Start()
    {
        StartCoroutine(InitializeUI());
    }

    private System.Collections.IEnumerator InitializeUI()
    {
        // Wait a frame to ensure CharacterBattleController runs Start() first
        yield return null;

        if (playerController == null)
        {
            Debug.LogError("❌ Player CharacterBattleController not assigned in BattleUIManager!");
            yield break;
        }

        playerRuntime = playerController.GetRuntimeCharacter();

        if (playerRuntime == null)
        {
            Debug.LogError("❌ playerRuntime is still null — CharacterBattleController may not have initialized correctly.");
            yield break;
        }

        // Hook up buttons
        attackButton.onClick.AddListener(OnAttackPressed);
        retreatButton.onClick.AddListener(OnRetreatPressed);

        // Hide attack panel at start
        attackSelectionPanel.SetActive(false);

        Debug.Log("✅ Battle UI initialized successfully with playerRuntime.");
    }

    void OnAttackPressed()
    {
        // Switch panels
        mainActionPanel.SetActive(false);
        attackSelectionPanel.SetActive(true);

        UpdateAttackButtons();
        Debug.Log($"🟢 Showing Attack Panel: {attackSelectionPanel.activeSelf}");
    }

    void OnRetreatPressed()
    {
        Debug.Log("🏃 Player chose to retreat!");
        // TODO: Add retreat logic later
    }

    void UpdateAttackButtons()
    {
        if (playerRuntime == null)
        {
            Debug.LogError("❌ playerRuntime is NULL — did you assign the PlayerController in the Inspector?");
            return;
        }

        if (attackButtons == null || attackButtons.Count == 0)
        {
            Debug.LogError("❌ attackButtons list is EMPTY — assign buttons in the Inspector!");
            return;
        }

        var attacks = playerRuntime.equippedAttacks;
        if (attacks == null)
        {
            Debug.LogError("❌ playerRuntime.equippedAttacks is NULL!");
            return;
        }

        for (int i = 0; i < attackButtons.Count; i++)
        {
            var button = attackButtons[i];

            if (i < attacks.Count && attacks[i] != null)
            {
                var attackData = attacks[i];
                button.gameObject.SetActive(true);

                var tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null)
                    tmpText.text = attackData.attackName;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnAttackChosen(attackData));

                Debug.Log($"✅ Assigned '{attackData.attackName}' to Button {i}");
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }

        Debug.Log($"🟢 Finished updating attack buttons ({attacks.Count} available).");
    }

    void OnAttackChosen(AttackData attack)
    {
        if (currentTarget == null)
        {
            Debug.LogWarning("⚠️ No enemy selected! Click an enemy to target it first.");
            return;
        }

        Debug.Log($"🎯 {playerRuntime.baseData.characterName} used {attack.attackName} on {currentTarget.characterData.characterName}!");

        // Example of reducing usage
        if (attack.currentUsage > 0)
            attack.currentUsage--;

        // Example placeholder for damage logic
        int basePower = attack.power;
        Debug.Log($"💥 Damage calculation pending: Base Power {basePower}");

        // After attacking, return to main panel
        attackSelectionPanel.SetActive(false);
        mainActionPanel.SetActive(true);
    }

    // ✅ Called by BattleManager when setting the player
    public void SetPlayerController(CharacterBattleController controller)
    {
        playerController = controller;

        if (playerController == null)
        {
            Debug.LogError("❌ Tried to set a null player controller in BattleUIManager!");
            return;
        }

        playerRuntime = playerController.GetRuntimeCharacter();

        if (playerRuntime == null)
        {
            Debug.LogError("❌ playerRuntime still null after SetPlayerController — CharacterBattleController not initialized?");
            return;
        }

        Debug.Log($"✅ Player controller linked to UI: {playerRuntime.baseData.characterName}");
        UpdateAttackButtons();
    }

    // ✅ Called by EnemySelector when an enemy is clicked
    public void SetTarget(CharacterBattleController target)
    {
        if (target == null)
            return;

        // Clear old highlight
        if (currentTarget != null && currentTarget.TryGetComponent(out EnemySelector prevSel))
            prevSel.Highlight(false);

        // Set new target
        currentTarget = target;
        if (currentTarget.TryGetComponent(out EnemySelector newSel))
            newSel.Highlight(true);

        Debug.Log($"🎯 Target selected: {currentTarget.characterData.characterName}");
    }
}
