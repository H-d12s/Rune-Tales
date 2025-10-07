using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainActionPanel;
    public GameObject attackSelectionPanel;

    [Header("Buttons")]
    public Button attackButton;
    public Button retreatButton;
    public List<Button> attackButtons;

    [Header("References")]
    public CharacterBattleController playerController;

    private CharacterRuntime playerRuntime;
    private CharacterBattleController currentTarget;
    private BattleManager battleManager;

    private bool isSelectingTarget = false;
    private AttackData selectedAttack;

    // Callback from BattleManager
    private Action<AttackData, CharacterBattleController> onAttackConfirmed;

    // =====================================================================
    // 🏁 INITIALIZATION
    // =====================================================================
    void Start()
    {
        StartCoroutine(InitializeUI());
    }

    private IEnumerator InitializeUI()
    {
        yield return null;

        battleManager = FindFirstObjectByType<BattleManager>();

        // ✅ Panels
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);
        if (mainActionPanel) mainActionPanel.SetActive(true); // visible at start

        // ✅ Buttons
        if (attackButton)
            attackButton.onClick.AddListener(OnAttackPressed);

        if (retreatButton)
            retreatButton.onClick.AddListener(OnRetreatPressed);
    }

    // =====================================================================
    // 🎮 PLAYER COMMAND PHASE
    // =====================================================================
    public void BeginPlayerChoice(Action<AttackData, CharacterBattleController> callback)
    {
        onAttackConfirmed = callback;
        ShowMainActions();
    }

    public void SetPlayerController(CharacterBattleController controller)
    {
        playerController = controller;
        playerRuntime = controller.GetRuntimeCharacter();
        UpdateAttackButtons();
    }

    // =====================================================================
    // ⚔️ ATTACK BUTTON HANDLING
    // =====================================================================
    private void OnAttackPressed()
    {
        if (mainActionPanel) mainActionPanel.SetActive(false);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(true);
        UpdateAttackButtons();
    }

    private void OnRetreatPressed()
    {
        Debug.Log("🏃 Retreat pressed (todo)");
    }

    private void UpdateAttackButtons()
    {
        var attacks = playerRuntime?.equippedAttacks;
        if (attacks == null) return;

        for (int i = 0; i < attackButtons.Count; i++)
        {
            var button = attackButtons[i];
            if (i < attacks.Count)
            {
                var attack = attacks[i];
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TextMeshProUGUI>().text = attack.attackName;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnAttackChosen(attack));
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }
    }

    private void OnAttackChosen(AttackData attack)
    {
        if (attack == null)
        {
            Debug.LogWarning("⚠️ Attack missing!");
            return;
        }

        selectedAttack = attack;
        isSelectingTarget = true;
        Debug.Log($"🌀 {playerRuntime.baseData.characterName} chose {attack.attackName}! Now select a target...");
    }

    // =====================================================================
    // 🎯 TARGET SELECTION
    // =====================================================================
    public void SetTarget(CharacterBattleController target)
    {
        if (!isSelectingTarget || target.isPlayer)
            return;

        currentTarget = target;
        isSelectingTarget = false;

        Debug.Log($"🎯 Target selected: {target.characterData.characterName}");
        Debug.Log($"⚔️ {playerController.characterData.characterName} attacks {target.characterData.characterName}!");

        // ✅ Confirm to BattleManager
        if (selectedAttack != null)
            onAttackConfirmed?.Invoke(selectedAttack, currentTarget);
        else
            Debug.LogWarning("⚠️ No attack selected before choosing target!");

        // Reset flicker/highlight
        var selector = target.GetComponent<EnemySelector>();
        if (selector != null)
            selector.Highlight(false);

        HideAll();
        selectedAttack = null;
    }

    // =====================================================================
    // 🧩 PANEL CONTROL
    // =====================================================================
    public void HideAll()
    {
        if (mainActionPanel) mainActionPanel.SetActive(false);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);

        isSelectingTarget = false;
        currentTarget = null;
        selectedAttack = null;
    }

    public void ShowMainActions()
    {
        if (mainActionPanel) mainActionPanel.SetActive(true);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);

        isSelectingTarget = false;
        currentTarget = null;
        selectedAttack = null;
    }

    // =====================================================================
    // ⚙️ ACCESSORS
    // =====================================================================
    public bool IsSelectingTarget() => isSelectingTarget;
}
