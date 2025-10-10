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
    public Button persuadeButton; // assigned in Inspector
    public List<Button> attackButtons;

    [Header("References")]
    public CharacterBattleController playerController;

    private CharacterRuntime playerRuntime;
    private CharacterBattleController currentTarget;
    private BattleManager battleManager;

    private bool isSelectingTarget = false;
    private AttackData selectedAttack;

    // public callbacks that BattleManager assigns when a player's turn begins
    [HideInInspector] public Action<AttackData, CharacterBattleController> onAttackConfirmed;
    [HideInInspector] public Action onPersuadeRequested; // NEW: invoked when Persuade button pressed

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

        if (attackButton)
            attackButton.onClick.AddListener(OnAttackPressed);

        if (retreatButton)
            retreatButton.onClick.AddListener(OnRetreatPressed);

        if (persuadeButton)
            persuadeButton.onClick.AddListener(OnPersuadePressed);

        // By default persuade button hidden until recruitment mode starts
        if (persuadeButton != null)
            persuadeButton.gameObject.SetActive(false);
    }

    // ======================================================
    // ATTACK UI
    // ======================================================
    private void OnAttackPressed()
    {
        if (mainActionPanel) mainActionPanel.SetActive(false);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(true);
        UpdateAttackButtons();
    }

    private void OnRetreatPressed()
    {
        Debug.Log("🏃 Retreat pressed (todo: implement retreat logic)");
    }

    // ======================================================
    // PERSUADE BUTTON (calls callback assigned by BattleManager)
    // ======================================================
    private void OnPersuadePressed()
    {
        // If a callback was assigned by BattleManager for the active character, invoke it.
        // This ensures only the active character's persuade is processed.
        if (onPersuadeRequested != null)
        {
            onPersuadeRequested.Invoke();
            return;
        }

        // Fallback behavior: if no callback is set, call BattleManager directly (backwards compatibility)
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
    // ATTACK TARGET SELECTION
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
        isSelectingTarget = true;
        Debug.Log($"🌀 {playerRuntime.baseData.characterName} chose {attack.attackName}! Now select a target...");
    }

    // Called by enemy selection code when player selects an enemy target
    public void SetTarget(CharacterBattleController target)
    {
        if (!isSelectingTarget || target == null || target.isPlayer)
            return;

        currentTarget = target;
        isSelectingTarget = false;

        if (selectedAttack != null)
            onAttackConfirmed?.Invoke(selectedAttack, currentTarget);

        var selector = target.GetComponent<EnemySelector>();
        if (selector != null)
            selector.Highlight(false);

        HideAll();
        selectedAttack = null;
    }

    // ======================================================
    // UI panels
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

    // ======================================================
    // persuade button toggle
    // ======================================================
    public void SetPersuadeButtonActive(bool active)
    {
        if (persuadeButton != null)
            persuadeButton.gameObject.SetActive(active);
    }

    // ======================================================
    // getters
    // ======================================================
    public bool IsSelectingTarget() => isSelectingTarget;
}
