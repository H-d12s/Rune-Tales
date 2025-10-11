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
    public Button persuadeButton; // optional, assigned in Inspector
    public List<Button> attackButtons;

    [Header("References")]
    public CharacterBattleController playerController;

    private CharacterRuntime playerRuntime;
    private CharacterBattleController currentTarget;
    private BattleManager battleManager;

    private bool isSelectingTarget = false;
    private AttackData selectedAttack;

    // Callbacks assigned by BattleManager
    [HideInInspector] public Action<AttackData, CharacterBattleController> onAttackConfirmed;
    [HideInInspector] public Action onPersuadeRequested;

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
        if (persuadeButton) persuadeButton.onClick.AddListener(OnPersuadePressed);

        if (persuadeButton != null)
            persuadeButton.gameObject.SetActive(false);
    }

    // ======================================================
    // Player Turn Setup
    // ======================================================
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

    // ======================================================
    // Button Handlers
    // ======================================================
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

    private void OnPersuadePressed()
    {
        if (onPersuadeRequested != null)
        {
            onPersuadeRequested.Invoke();
            return;
        }

        if (battleManager == null)
        {
            Debug.LogError("❌ BattleManager not found for persuasion!");
            return;
        }

        var enemies = battleManager.GetAllEnemies();
        if (enemies == null || enemies.Count == 0)
        {
            Debug.LogWarning("⚠️ No enemies available to persuade!");
            return;
        }

        CharacterBattleController target = enemies.Find(e => e != null && e.GetRuntimeCharacter().IsAlive);
        if (target == null)
        {
            Debug.LogWarning("⚠️ No valid persuasion targets!");
            return;
        }

        Debug.Log($"🗣️ Attempting to persuade {target.characterData.characterName} (fallback).");
        battleManager.TryPersuade(target);
    }

    // ======================================================
    // Attack Buttons & Selection
    // ======================================================
    private void UpdateAttackButtons()
    {
        playerRuntime = playerController != null ? playerController.GetRuntimeCharacter() : null;
        var attacks = playerRuntime?.equippedAttacks;
        if (attacks == null) return;

        for (int i = 0; i < attackButtons.Count; i++)
        {
            var button = attackButtons[i];
            if (i < attacks.Count)
            {
                var attack = attacks[i];
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = attack.attackName;

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
        isSelectingTarget = false;

        // Auto-apply to self/allies
        if (attack.affectsSelf && !attack.manualBuffTargetSelection)
        {
            if (!attack.isAoE)
            {
                Debug.Log($"🌀 {playerRuntime.baseData.characterName} uses {attack.attackName} on self automatically!");
                onAttackConfirmed?.Invoke(selectedAttack, playerController);
                HideAll();
                selectedAttack = null;
                return;
            }
            else
            {
                Debug.Log($"🌀 {playerRuntime.baseData.characterName} uses {attack.attackName} on all allies automatically!");
                var allies = FindObjectsOfType<CharacterBattleController>();
                foreach (var ally in allies)
                {
                    if (ally.isPlayer && ally.GetRuntimeCharacter().IsAlive)
                        onAttackConfirmed?.Invoke(selectedAttack, ally);
                }
                HideAll();
                selectedAttack = null;
                return;
            }
        }

        // Otherwise, wait for target selection
        isSelectingTarget = true;
        if (attack.healsTarget || attack.manualBuffTargetSelection)
            Debug.Log($"🌀 {playerRuntime.baseData.characterName} chose {attack.attackName}! Select an ally target...");
        else
            Debug.Log($"🌀 {playerRuntime.baseData.characterName} chose {attack.attackName}! Select an enemy target...");
    }

    // ======================================================
    // Target Selection
    // ======================================================
    public void SetTarget(CharacterBattleController target)
    {
        if (!isSelectingTarget || target == null || selectedAttack == null) return;

        bool isValid = false;
        if (selectedAttack.healsTarget || selectedAttack.manualBuffTargetSelection)
        {
            if (target.isPlayer) isValid = true;
        }
        else
        {
            if (!target.isPlayer) isValid = true;
        }
        if (!isValid) return;

        currentTarget = target;
        isSelectingTarget = false;

        Debug.Log($"🎯 Target selected: {target.characterData.characterName}");
        onAttackConfirmed?.Invoke(selectedAttack, currentTarget);

        var selector = target.GetComponent<TargetSelector>();
        if (selector != null) selector.Highlight(false);

        HideAll();
        selectedAttack = null;
    }

    // ======================================================
    // UI Panels
    // ======================================================
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

    public AttackData SelectedAttack() => selectedAttack;

    public void SetPersuadeButtonActive(bool active)
    {
        if (persuadeButton != null)
            persuadeButton.gameObject.SetActive(active);
    }

    public bool IsSelectingTarget() => isSelectingTarget;
}
