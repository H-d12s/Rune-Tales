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

    void Start()
    {
        StartCoroutine(InitializeUI());
    }

    private IEnumerator InitializeUI()
    {
        yield return null;
        battleManager = FindFirstObjectByType<BattleManager>();

        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);
        if (mainActionPanel) mainActionPanel.SetActive(true);

        if (attackButton) attackButton.onClick.AddListener(OnAttackPressed);
        if (retreatButton) retreatButton.onClick.AddListener(OnRetreatPressed);
    }

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
        if (attack == null) return;

        selectedAttack = attack;
        isSelectingTarget = false; // default

        // --- Auto-apply to self ---
        if (attack.affectsSelf && !attack.manualBuffTargetSelection)
        {
            if (!attack.isAoE)
            {
                // Single target self
                Debug.Log($"🌀 {playerRuntime.baseData.characterName} uses {attack.attackName} on self automatically!");
                onAttackConfirmed?.Invoke(selectedAttack, playerController);

                // Hide UI now that the player has confirmed this attack
                HideAll();

                selectedAttack = null;
                return;
            }
            else
            {
                // AoE self/allies
                Debug.Log($"🌀 {playerRuntime.baseData.characterName} uses {attack.attackName} on all allies automatically!");
                var allies = FindObjectsOfType<CharacterBattleController>();
                foreach (var ally in allies)
                {
                    if (ally.isPlayer && ally.GetRuntimeCharacter().IsAlive)
                    {
                        onAttackConfirmed?.Invoke(selectedAttack, ally);
                    }
                }

                // Hide UI after firing all confirmations
                HideAll();

                selectedAttack = null;
                return;
            }
        }

        // Otherwise, wait for player to select a target
        isSelectingTarget = true;
        if (attack.healsTarget || attack.manualBuffTargetSelection)
            Debug.Log($"🌀 {playerRuntime.baseData.characterName} chose {attack.attackName}! Select an ally target...");
        else
            Debug.Log($"🌀 {playerRuntime.baseData.characterName} chose {attack.attackName}! Select an enemy target...");
    }

    // Target Selection
    public void SetTarget(CharacterBattleController target)
    {
        if (!isSelectingTarget || target == null || selectedAttack == null) return;

        bool isValid = false;

        if (selectedAttack.healsTarget || selectedAttack.manualBuffTargetSelection)
        {
            // Target must be an ally
            if (target.isPlayer) isValid = true;
        }
        else
        {
            // Target must be an enemy
            if (!target.isPlayer) isValid = true;
        }

        if (!isValid) return;

        currentTarget = target;
        isSelectingTarget = false;

        Debug.Log($"🎯 Target selected: {target.characterData.characterName}");

        onAttackConfirmed?.Invoke(selectedAttack, currentTarget);

        // Reset highlight
        var selector = target.GetComponent<TargetSelector>();
        if (selector != null) selector.Highlight(false);

        // Hide UI once the player has confirmed a target
        HideAll();
        selectedAttack = null;
    }

    // Panel Control
    public void HideAll()
    {
        if (mainActionPanel) mainActionPanel.SetActive(false);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);

        isSelectingTarget = false;
        currentTarget = null;
        selectedAttack = null;
    }

    public AttackData SelectedAttack()
    {
        return selectedAttack;
    }

    public void ShowMainActions()
    {
        if (mainActionPanel) mainActionPanel.SetActive(true);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);

        isSelectingTarget = false;
        currentTarget = null;
        selectedAttack = null;
    }

    // Accessors
    public bool IsSelectingTarget() => isSelectingTarget;
}
