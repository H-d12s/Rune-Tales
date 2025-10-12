using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal move-replace prompt:
/// - ShowReplacePrompt(characterName, moves, newMoveName) displays a persistent message in the BattleMessageUI panel.
/// - Wait for 1..9 (0-based index stored in LastSelectedIndex) or N to cancel.
/// - After selection it shows a short confirmation message then hides the persistent prompt.
/// </summary>
public class MoveReplaceUIManager : MonoBehaviour
{
    public static MoveReplaceUIManager Instance { get; private set; }

    // Public read-only state for callers
    public bool IsAwaitingChoice { get; private set; } = false;
    public int LastSelectedIndex { get; private set; } = -1; // 0-based, -1 = none
    public bool WasCancelled { get; private set; } = false;

    // Optional: assign in inspector or auto-find at Awake
    public BattleMessageUI messageUI;

    private List<string> currentMoves;
    private string currentNewMoveName;
    private string currentCharacterName;

    // cached reference to BattleManager so we can safely clear message queue when showing/hiding persistent prompts
    private BattleManager battleManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (messageUI == null)
            messageUI = FindObjectOfType<BattleMessageUI>();

        battleManager = FindObjectOfType<BattleManager>();
    }

    /// <summary>
    /// Show the small persistent prompt that lists the moves and waits for 1..9 or N.
    /// Example output (for 2 moves):
    /// "Hades can now learn a new attack (Rend)
    /// Press 1 to replace Slash. Press 2 to replace Shield Bash. Press N to exit and continue."
    /// </summary>
   public void ShowReplacePrompt(string characterName, List<string> moves, string newMoveName)
{
    currentCharacterName = string.IsNullOrEmpty(characterName) ? "(unknown)" : characterName;
    currentMoves = moves != null ? new List<string>(moves) : new List<string>();
    currentNewMoveName = string.IsNullOrEmpty(newMoveName) ? "(unknown)" : newMoveName;

    LastSelectedIndex = -1;
    WasCancelled = false;
    IsAwaitingChoice = true;

    // Build minimal prompt (player name, new move, which numbers to press)
    string prompt = $"{currentCharacterName} can now learn a new attack ({currentNewMoveName})\n";
    for (int i = 0; i < currentMoves.Count && i < 9; i++)
    {
        prompt += $"Press {i + 1} to replace {currentMoves[i]}. ";
    }
    prompt += "Press N to exit and continue.";

    if (messageUI == null) messageUI = FindObjectOfType<BattleMessageUI>();
    if (battleManager == null) battleManager = FindObjectOfType<BattleManager>();

    // Use BattleManager to cancel/hide any queued messages so we won't race with a typed coroutine.
    try
    {
        if (battleManager != null)
            battleManager.CancelAndHideBattleMessage();
    }
    catch { /* defensive */ }

    // Use SetPersistentMessage to keep the panel visible until HidePersistentMessage is called.
    if (messageUI != null)
        messageUI.SetPersistentMessage(prompt);
    else
        Debug.Log(prompt);

    // Also show button indicators via BattleUIManager (optional / visual)
    var ui = FindObjectOfType<BattleUIManager>();
    if (ui != null)
        ui.ShowReplaceIndicators(currentMoves, currentNewMoveName);
}


    private void Update()
    {
        if (!IsAwaitingChoice) return;

        if (currentMoves == null || currentMoves.Count == 0)
        {
            CancelChoice();
            return;
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            CancelChoice();
            return;
        }

        for (int i = 0; i < currentMoves.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectIndex(i);
                return;
            }
        }
    }

    // inside MoveReplaceUIManager

// --- Place inside MoveReplaceUIManager ---

private void RemoveReplaceIndicatorsFromUI()
{
    // Try the more modern FindFirstObjectByType if available, fallback to FindObjectOfType
    BattleUIManager ui = null;
    try { ui = FindFirstObjectByType<BattleUIManager>(); } catch { ui = null; }
    if (ui == null) ui = FindObjectOfType<BattleUIManager>();

    if (ui != null)
    {
        try { ui.ClearReplaceIndicators(); } catch (System.Exception ex) { Debug.LogWarning($"Failed to clear replace indicators: {ex.Message}"); }
    }
    else
    {
        Debug.Log("MoveReplaceUIManager: BattleUIManager not found when trying to clear replace indicators.");
    }
}

private IEnumerator HideMessageAfterDelay(float delay)
{
    // Wait a bit then hide persistent message (defensive)
    yield return new WaitForSeconds(Mathf.Max(0f, delay));
    if (battleManager != null)
        battleManager.CancelAndHideBattleMessage();
    else if (messageUI != null)
        messageUI.HidePersistentMessage();
}

/// <summary>
/// Call when player presses a number to pick which move to replace.
/// </summary>
private void SelectIndex(int index)
{
    LastSelectedIndex = index;
    WasCancelled = false;
    IsAwaitingChoice = false;

    // Clear persistent prompt (so the panel will not remain locked)
    if (battleManager != null)
        battleManager.CancelAndHideBattleMessage();
    else if (messageUI != null)
        messageUI.HidePersistentMessage();

    // Clear replace indicators on attack buttons
    var ui = FindObjectOfType<BattleUIManager>();
    if (ui != null)
    {
        try { ui.ClearReplaceIndicators(); } catch { }
    }

    // Show a short confirmation via the BattleManager (non-blocking queued message)
    string confirmation = $"{(currentMoves != null && index < currentMoves.Count ? currentMoves[index] : "(unknown)")} replaced with {currentNewMoveName}!";
    if (battleManager != null)
    {
        // non-blocking so we don't stall other flows
        battleManager.StartCoroutine(battleManager.ShowBattleMessage(confirmation, false));
    }
    else if (messageUI != null)
    {
        // fallback to direct instant show, then hide after delay
        messageUI.ShowMessageInstant(confirmation);
        StartCoroutine(HideConfirmationAfterDelay(messageUI.messageStayTime));
    }
    else
    {
        Debug.Log(confirmation);
    }

    Debug.Log($"MoveReplaceUIManager: player chose index {index} (move '{(currentMoves != null && index < currentMoves.Count ? currentMoves[index] : "(unknown)")}').");
}

private void CancelChoice()
{
    LastSelectedIndex = -1;
    WasCancelled = true;
    IsAwaitingChoice = false;

    // Ensure persistent prompt is cleared and indicators are removed
    if (battleManager != null)
        battleManager.CancelAndHideBattleMessage();
    else if (messageUI != null)
        messageUI.HidePersistentMessage();

    var ui = FindObjectOfType<BattleUIManager>();
    if (ui != null)
    {
        try { ui.ClearReplaceIndicators(); } catch { }
    }

    Debug.Log($"MoveReplaceUIManager: player cancelled move learn of {currentNewMoveName} for {currentCharacterName}.");
}

// Programmatic helpers
public void ForceSelectIndex(int index) 
{
    if (!IsAwaitingChoice) return;
    if (currentMoves == null || index < 0 || index >= currentMoves.Count) return;
    SelectIndex(index);
}
public void ForceCancel() 
{
    if (!IsAwaitingChoice) return;
    CancelChoice();
}

private IEnumerator HideConfirmationAfterDelay(float delay)
{
    yield return new WaitForSeconds(Mathf.Max(0f, delay));
    if (battleManager != null)
        battleManager.CancelAndHideBattleMessage();
    else if (messageUI != null)
        messageUI.HidePersistentMessage(); // works as a HideInstant equivalent
}
}
