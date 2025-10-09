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
    /// </summary>
    /// <param name="moves">Current move names</param>
    /// <param name="newMove">The new move to learn</param>
    /// <param name="onReplaceChosen">Callback: player picks index to replace</param>
    /// <param name="onCancelChoice">Callback: player declines learning new move</param>
    public void ShowReplacePrompt(List<string> moves, string newMove, Action<int> onReplaceChosen, Action onCancelChoice)
    {
        currentMoves = new List<string>(moves);
        newMoveName = newMove;
        onMoveSelected = onReplaceChosen;
        onCancel = onCancelChoice;
        awaitingChoice = true;

        Debug.Log($"🧠 {newMove} is ready to be learned!");
        Debug.Log($"You already know {moves.Count} moves:");
        for (int i = 0; i < moves.Count; i++)
            Debug.Log($"[{i + 1}] {moves[i]}");

        Debug.Log($"Press the number (1–{moves.Count}) of the move you want to replace, or press N to cancel.");
    }

    private void Update()
    {
        if (!awaitingChoice) return;

        // Cancel learning
        if (Input.GetKeyDown(KeyCode.N))
        {
            awaitingChoice = false;
            Debug.Log($"❌ You decided NOT to learn {newMoveName}.");
            onCancel?.Invoke();
            return;
        }

        // Replace one of the moves
        for (int i = 0; i < currentMoves.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                awaitingChoice = false;
                Debug.Log($"✅ Replacing {currentMoves[i]} with {newMoveName}.");
                onMoveSelected?.Invoke(i);
                return;
            }
        }
    }
}
