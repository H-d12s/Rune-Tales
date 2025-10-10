using System.Collections.Generic;
using UnityEngine;
using System;

public class MoveReplaceUIManager : MonoBehaviour
{
    public static MoveReplaceUIManager Instance { get; private set; }

    private Action<int> onMoveSelected;
    private Action onCancel;
    private string newMoveName;
    private List<string> currentMoves;
    private bool awaitingChoice = false;

    // Public readable properties so coroutines can poll/wait
    public bool IsAwaitingChoice => awaitingChoice;
    public int LastSelectedIndex { get; private set; } = -1; // -1 = none / cancelled
    public bool WasCancelled { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Opens a simple text-based UI for replacing a move.
    /// Callers may pass null callbacks if they want to poll LastSelectedIndex / WasCancelled instead.
    /// </summary>
    public void ShowReplacePrompt(List<string> moves, string newMove, Action<int> onReplaceChosen, Action onCancelChoice)
    {
        currentMoves = new List<string>(moves);
        newMoveName = newMove;
        onMoveSelected = onReplaceChosen;
        onCancel = onCancelChoice;
        awaitingChoice = true;
        LastSelectedIndex = -1;
        WasCancelled = false;

        Debug.Log($"🧠 {newMove} is ready to be learned!");
        Debug.Log($"You already know {moves.Count} moves:");
        for (int i = 0; i < moves.Count; i++)
            Debug.Log($"[{i + 1}] {moves[i]}");

        Debug.Log($"Press the number (1–{moves.Count}) of the move you want to replace, or press N to cancel.");
    }

   private void Update()
{
    if (!awaitingChoice) return;
    if (currentMoves == null || currentMoves.Count == 0)
    {
        // No choices: auto-cancel to avoid indefinite waits
        awaitingChoice = false;
        WasCancelled = true;
        Debug.LogWarning("⚠️ MoveReplaceUIManager: no current moves to replace — auto-cancelling.");
        onCancel?.Invoke();
        onCancel = null;
        onMoveSelected = null;
        return;
    }

    // Cancel learning
    if (Input.GetKeyDown(KeyCode.N))
    {
        awaitingChoice = false;
        WasCancelled = true;
        Debug.Log($"❌ You decided NOT to learn {newMoveName}.");
        onCancel?.Invoke();
        onCancel = null;
        onMoveSelected = null;
        return;
    }

    // Replace one of the moves (supports up to 9 keys via Alpha1..Alpha9)
    for (int i = 0; i < currentMoves.Count && i < 9; i++)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1 + i))
        {
            awaitingChoice = false;
            LastSelectedIndex = i;
            Debug.Log($"✅ Replacing {currentMoves[i]} with {newMoveName}.");
            onMoveSelected?.Invoke(i);
            onCancel = null;
            onMoveSelected = null;
            return;
        }
    }
}

// Optional helpers for programmatic control (useful for tests or UI buttons)
public void ForceCancel()
{
    if (!awaitingChoice) return;
    awaitingChoice = false;
    WasCancelled = true;
    onCancel?.Invoke();
    onCancel = null;
    onMoveSelected = null;
    Debug.Log("MoveReplaceUIManager: ForceCancel called.");
}

public void ForceSelectIndex(int index)
{
    if (!awaitingChoice) return;
    if (currentMoves == null || index < 0 || index >= currentMoves.Count) return;
    awaitingChoice = false;
    LastSelectedIndex = index;
    onMoveSelected?.Invoke(index);
    onCancel = null;
    onMoveSelected = null;
    Debug.Log($"MoveReplaceUIManager: ForceSelectIndex {index} called.");
}
}
