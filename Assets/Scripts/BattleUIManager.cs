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
    [HideInInspector] public Action onPersuadeChosen;

    public GameObject attackSelectionPanel;

    [Header("Buttons")]
    public Button attackButton;
    public Button retreatButton;
    public Button persuadeButton; // 🆕 For recruitment encounters
    public List<Button> attackButtons;

    [Header("References")]
    public CharacterBattleController playerController;

    private CharacterRuntime playerRuntime;
    private CharacterBattleController currentTarget;
    private BattleManager battleManager;

    private bool isSelectingTarget = false;
    private AttackData selectedAttack;

    // 🧩 This is now public so BattleManager can subscribe to it
    [HideInInspector] public Action<AttackData, CharacterBattleController> onAttackConfirmed;


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
    // ⚔️ ATTACK LOGIC
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
    // 💬 PERSUASION LOGIC (Recruitment mode)
    // ======================================================
    private void OnPersuadePressed()
{
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

    Debug.Log($"🗣️ Attempting to persuade {target.characterData.characterName}...");
    battleManager.TryPersuade(target);

    // 🔹 Tell BattleManager that the turn for this character is done
    onPersuadeChosen?.Invoke();

    HideAll(); // close menus to visually show turn ended
}

    // ======================================================
    // 🎯 ATTACK TARGET SELECTION
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

    public void SetTarget(CharacterBattleController target)
    {
        if (!isSelectingTarget || target.isPlayer)
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
    // PANEL CONTROL
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
    // UI Helper for Persuasion Mode Toggle
    // ======================================================
    public void SetPersuadeButtonActive(bool active)
    {
        if (persuadeButton != null)
            persuadeButton.gameObject.SetActive(active);
    }

    // ======================================================
    // GETTERS
    // ======================================================
    public bool IsSelectingTarget() => isSelectingTarget;
}
