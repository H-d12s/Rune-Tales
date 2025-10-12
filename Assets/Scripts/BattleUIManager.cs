using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Battle UI manager: main actions, attack selection, and visual "replace" indicators
/// used by MoveReplaceUIManager/ExperienceSystem.
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainActionPanel;
    public GameObject attackSelectionPanel;

    [Header("Replace UI")]
    [Tooltip("Optional prefab shown over an attack button to indicate which move is replaced. " +
             "Prefab should contain a TextMeshProUGUI for the label (or it'll be created at runtime).")]
    public GameObject replaceIndicatorPrefab;

    // runtime list of indicators we created (so we can clear them)
    private List<GameObject> activeReplaceIndicators = new List<GameObject>();

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
        // wait a frame to let scene objects register
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
        playerRuntime = controller != null ? controller.GetRuntimeCharacter() : null;
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
        // Clear any leftover replace indicators when attack buttons refresh
        ClearReplaceIndicators();

        playerRuntime = playerController != null ? playerController.GetRuntimeCharacter() : null;
        var attacks = playerRuntime?.equippedAttacks;
        if (attackButtons == null || attackButtons.Count == 0) return;

        // If no runtime (e.g. dead/uninitialized), hide all attack buttons
        if (attacks == null || attacks.Count == 0)
        {
            for (int i = 0; i < attackButtons.Count; i++)
            {
                var btn = attackButtons[i];
                if (btn != null) btn.gameObject.SetActive(false);
            }
            return;
        }

        for (int i = 0; i < attackButtons.Count; i++)
        {
            var button = attackButtons[i];
            if (button == null) continue;

            if (i < attacks.Count)
            {
                // capture locally to avoid closure issues
                var attack = attacks[i];
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = attack != null ? attack.attackName : "(unknown)";

                // remove previous listeners then add a fresh one that captures "attack"
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnAttackChosen(attack));
            }
            else
            {
                button.onClick.RemoveAllListeners();
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
    // Replace Indicators
    // ======================================================
    /// <summary>
    /// Show small numeric indicators over each attack button and present a persistent message in the message panel.
    /// moveNames should be the list of currently equipped move names (in order). newMoveName shown in message.
    /// </summary>
   public void ShowReplaceIndicators(List<string> moveNames, string newMoveName)
{
    // defensive checks
    if (attackButtons == null || attackButtons.Count == 0)
        return;

    ClearReplaceIndicators(); // start fresh

    // Ensure attack panel is visible so indicators appear
    if (attackSelectionPanel != null && !attackSelectionPanel.activeInHierarchy)
        attackSelectionPanel.SetActive(true);

    int max = Mathf.Min(attackButtons.Count, moveNames != null ? moveNames.Count : 0);
    for (int i = 0; i < max; i++)
    {
        var btn = attackButtons[i];
        if (btn == null) continue;

        GameObject indicator = null;

        if (replaceIndicatorPrefab != null)
        {
            indicator = Instantiate(replaceIndicatorPrefab, btn.transform, false);
            // try to position top-right if RectTransform present
            var rt = indicator.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-8f, -8f);
                rt.localScale = Vector3.one;
            }
        }
        else
        {
            // fallback: create a small TMP label as child
            indicator = new GameObject($"ReplaceIndicator_{i + 1}", typeof(RectTransform));
            indicator.transform.SetParent(btn.transform, false);
            var rt = indicator.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(36f, 24f);

            var img = indicator.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0f, 0f, 0f, 0.6f);

            var tmpGO = new GameObject("Label", typeof(RectTransform));
            tmpGO.transform.SetParent(indicator.transform, false);
            var tmp = tmpGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = (i + 1).ToString();
            tmp.raycastTarget = false;
        }

        // If indicator has a TMP child, set text to index
        var tm = indicator.GetComponentInChildren<TextMeshProUGUI>();
        if (tm != null)
        {
            tm.text = (i + 1).ToString();
            tm.color = Color.white;
        }

        activeReplaceIndicators.Add(indicator);
    }

    // Also set a persistent message explaining controls (so keyboard users know)
    var msgUI = FindFirstObjectByType<BattleMessageUI>();
    var bm = FindFirstObjectByType<BattleManager>();
    try
    {
        if (bm != null)
        {
            bm.CancelAndHideBattleMessage(); // clear any queued/active message to avoid races
        }
    }
    catch { }

    if (msgUI != null)
    {
        msgUI.SetPersistentMessage($"{playerController?.characterData?.characterName ?? "Your player"} can now learn a new attack ({newMoveName}).\nPress 1 to replace { (moveNames.Count > 0 ? moveNames[0] : "(none)") }." +
                                   $"{(moveNames.Count > 1 ? $" Press 2 to replace {moveNames[1]}." : "")} Press N to exit and continue.");
    }
}


    /// <summary>
    /// Clear any replace indicators we created.
    /// </summary>
    public void ClearReplaceIndicators()
    {
        for (int i = 0; i < activeReplaceIndicators.Count; i++)
        {
            var go = activeReplaceIndicators[i];
            if (go != null) Destroy(go);
        }
        activeReplaceIndicators.Clear();

        // Do not automatically hide message panel here; MoveReplaceUIManager will hide persistent prompts.
        // But if you want to ensure we don't hold an accidental persistent message, you can uncomment below:
        // var msgUI = FindFirstObjectByType<BattleMessageUI>();
        // if (msgUI != null) msgUI.HidePersistentMessage();
    }

    // ======================================================
    // UI Panels
    // ======================================================
    public void HideAll()
    {
        if (mainActionPanel) mainActionPanel.SetActive(false);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);

        // clean up indicators so they don't persist across states
        ClearReplaceIndicators();

        isSelectingTarget = false;
        currentTarget = null;
        selectedAttack = null;
    }

    public void ShowMainActions()
    {
        if (mainActionPanel) mainActionPanel.SetActive(true);
        if (attackSelectionPanel) attackSelectionPanel.SetActive(false);

        // ensure indicators cleared when showing main actions
        ClearReplaceIndicators();

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
