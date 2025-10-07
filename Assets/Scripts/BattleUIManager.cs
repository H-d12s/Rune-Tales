using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainActionPanel;      // Panel with Attack / Equipment / Persuade / Retreat
    public GameObject attackSelectionPanel; // Panel with attack buttons
    private bool isSelectingTarget = false;
    [Header("Buttons")]
    public Button attackButton;
    public Button equipmentButton;
    public Button persuadeButton;
    public Button retreatButton;

    [Header("Attack Buttons")]
    public List<Button> attackButtons; // Assign 2–4 buttons here

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
        // Wait one frame so CharacterBattleController can initialize
        yield return null;

        if (playerController == null)
        {
            Debug.LogError("❌ Player CharacterBattleController not assigned in BattleUIManager!");
            yield break;
        }

        playerRuntime = playerController.GetRuntimeCharacter();

        if (playerRuntime == null)
        {
            Debug.LogError("❌ playerRuntime still null — CharacterBattleController may not have initialized correctly.");
            yield break;
        }

        // ✅ Hook up buttons
        attackButton.onClick.AddListener(OnAttackPressed);
        retreatButton.onClick.AddListener(OnRetreatPressed);

        attackSelectionPanel.SetActive(false); // hide attack panel at start

        Debug.Log("✅ Battle UI initialized successfully with playerRuntime.");
    }

    // --- ATTACK PANEL SWITCHING ---
    void OnAttackPressed()
    {
        mainActionPanel.SetActive(false);
        attackSelectionPanel.SetActive(true);
        UpdateAttackButtons();

        isSelectingTarget = true; // ✅ allow enemy clicks now

        Debug.Log("🟢 Showing Attack Panel — target selection enabled");
    }


    void OnRetreatPressed()
    {
        Debug.Log("🏃 Player chose to retreat! (Add logic later)");
    }

    // --- ATTACK BUTTON LOGIC ---
    void UpdateAttackButtons()
    {
        if (playerRuntime == null)
        {
            Debug.LogError("❌ playerRuntime is NULL!");
            return;
        }

        var attacks = playerRuntime.equippedAttacks;
        if (attacks == null)
        {
            Debug.LogError("❌ equippedAttacks is NULL!");
            return;
        }

        for (int i = 0; i < attackButtons.Count; i++)
        {
            var button = attackButtons[i];

            if (i < attacks.Count && attacks[i] != null)
            {
                var attackData = attacks[i];
                button.gameObject.SetActive(true);

                // Set name text
                var tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null)
                    tmpText.text = attackData.attackName;

                // Assign click
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnAttackChosen(attackData));
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }

        Debug.Log($"🟢 Updated attack buttons ({attacks.Count} total).");
    }

    // --- ATTACK EXECUTION ---
   void OnAttackChosen(AttackData attack)
{
    if (currentTarget == null)
    {
        Debug.LogWarning("⚠️ No enemy selected! Click an enemy to target it first.");
        return;
    }

    Debug.Log($"🎯 {playerRuntime.baseData.characterName} used {attack.attackName} on {currentTarget.characterData.characterName}!");

    if (attack.currentUsage > 0)
        attack.currentUsage--;

    // Placeholder for damage logic
    Debug.Log($"💥 Damage pending (Power: {attack.power})");

    // ✅ Reset color after the attack
    var selector = currentTarget.GetComponent<EnemySelector>();
    if (selector != null)
        selector.ResetHighlight();

    attackSelectionPanel.SetActive(false);
    mainActionPanel.SetActive(true);

    // ✅ Clear current target reference
    currentTarget = null;
}
    // --- PLAYER LINKING ---
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

    // --- ENEMY TARGETING ---
    public void SetTarget(CharacterBattleController target)
    {
        if (target == null || target.isPlayer)
            return;

        // Unhighlight old
        if (currentTarget != null && currentTarget.TryGetComponent(out EnemySelector prevSel))
            prevSel.Highlight(false);

        // Set new target
        currentTarget = target;
        if (currentTarget.TryGetComponent(out EnemySelector newSel))
            newSel.Highlight(true);

        Debug.Log($"🎯 Target selected: {currentTarget.characterData.characterName}");
    }
    public bool IsSelectingTarget()
{
    return isSelectingTarget;
}
}
