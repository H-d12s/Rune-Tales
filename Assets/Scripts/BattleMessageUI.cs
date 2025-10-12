using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Simple message panel with a typewriter ShowMessage coroutine plus
/// persistent prompt helpers used by MoveReplace/ExperienceSystem.
/// This implementation provides compatibility helpers:
/// - ShowMessageThenHideInstant(string)
/// - ShowMessageThenHideInstantCoroutine(string)
/// - ShowPersistentMessage(string)  (alias)
/// - HideInstant()                 (alias)
/// - ClearPersistentMessage()      (alias for HidePersistentMessage)
/// </summary>
public class BattleMessageUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject messagePanel;    // Assign your Message Panel GameObject
    public TMP_Text messageText;       // Assign your TMP text component

    [Header("Settings")]
    public float typeSpeed = 0.02f;       // Delay between characters
    public float messageStayTime = 1.0f;  // Time before message disappears when using instant-show helpers

    // The coroutine handle for the typed / helper coroutine so we can reliably stop it.
    private Coroutine typingCoroutine = null;
    private Coroutine activeRoutine = null;

    // persistent prompt flag
    private bool isPersistentMessageActive = false;

    private void Awake()
    {
        if (messageText != null) messageText.text = "";
        if (messagePanel != null) messagePanel.SetActive(false);
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";
        isPersistentMessageActive = false;
    }

    private void OnDestroy()
    {
        StopActiveRoutine();
    }

    private void StopActiveRoutine()
    {
        // Stop the typed coroutine if running
        if (typingCoroutine != null)
        {
            try { StopCoroutine(typingCoroutine); } catch { }
            typingCoroutine = null;
        }

        // Stop any helper coroutine recorded in activeRoutine (e.g. ShowMessageThenHideInstantCoroutine)
        if (activeRoutine != null)
        {
            try { StopCoroutine(activeRoutine); } catch { }
            activeRoutine = null;
        }
    }

    /// <summary>
    /// Typewriter-style message. Caller can StartCoroutine on this.
    /// </summary>
    public IEnumerator ShowMessage(string message)
    {
        // Stop previous typing & helper coroutines so messages do not clash
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";

        if (messagePanel == null || messageText == null)
        {
            Debug.LogWarning("⚠️ BattleMessageUI missing references!");
            yield break;
        }

        // Ensure panel active
        if (!messagePanel.activeInHierarchy) messagePanel.SetActive(true);

        // Start internal typing coroutine and keep its handle so it can be stopped externally
        typingCoroutine = StartCoroutine(TypeTextCoroutine(message));
        // also set activeRoutine to the same handle so other helpers stop it consistently
        activeRoutine = typingCoroutine;

        yield return typingCoroutine;

        // Clean up handles after finished
        typingCoroutine = null;
        activeRoutine = null;
        yield break;
    }

    private IEnumerator TypeTextCoroutine(string message)
{
    // Debug start
    Debug.Log($"[BattleMessageUI] TypeTextCoroutine START — '{(message ?? "").Replace("\n", "\\n")}'");

    // Typewriter
    if (messageText != null) messageText.text = "";

    foreach (char c in message)
    {
        if (messageText != null) messageText.text += c;
        // use real-time so typing continues if timeScale == 0
        yield return new WaitForSecondsRealtime(typeSpeed);
    }

    // Pause for readability (real-time)
    yield return new WaitForSecondsRealtime(messageStayTime);

    // Only auto-hide if not persistent
    if (!isPersistentMessageActive)
    {
        if (messagePanel != null) messagePanel.SetActive(false);
        if (messageText != null) messageText.text = "";
    }

    // Debug end
    Debug.Log($"[BattleMessageUI] TypeTextCoroutine END — finished message.");
}


    /// <summary>
    /// Show instantly (no typing). Use from any code.
    /// </summary>
    public void ShowMessageInstant(string message)
    {
        StopActiveRoutine();
        if (messagePanel == null || messageText == null) return;
        messagePanel.SetActive(true);
        messageText.text = message ?? "";
        // nothing left to stop; we are instant
    }

    /// <summary>
    /// Show a message immediately and keep the panel visible until HidePersistentMessage is called.
    /// Intended for prompts that wait for player input (replace prompts).
    /// A compatibility alias ShowPersistentMessage is also provided.
    /// </summary>
    public void SetPersistentMessage(string text)
    {
        StopActiveRoutine();

        if (messageText != null)
            messageText.text = text ?? "";

        if (messagePanel != null && !messagePanel.activeInHierarchy)
            messagePanel.SetActive(true);

        isPersistentMessageActive = true;
    }

    /// <summary>
    /// Alias kept for compatibility with other scripts that call ShowPersistentMessage.
    /// </summary>
    public void ShowPersistentMessage(string text)
    {
        SetPersistentMessage(text);
    }

    /// <summary>
    /// Hide the persistent message and clear text. Alias ClearPersistentMessage kept for compatibility.
    /// </summary>
    public void HidePersistentMessage()
    {
        StopActiveRoutine();

        if (messageText != null) messageText.text = "";
        if (messagePanel != null && messagePanel.activeInHierarchy) messagePanel.SetActive(false);

        isPersistentMessageActive = false;
    }

    /// <summary>
    /// Compatibility alias.
    /// </summary>
    public void ClearPersistentMessage()
    {
        HidePersistentMessage();
    }

    /// <summary>
    /// Query whether a persistent message is showing.
    /// </summary>
    public bool IsPersistentMessageActive() => isPersistentMessageActive;

    /// <summary>
    /// Convenience: clear text (both TMP and stop any typing) in a safe manner.
    /// Useful for callers that want to ensure nothing lingers visually.
    /// </summary>
    public void ClearTextSafely()
    {
        try
        {
            StopActiveRoutine();

            if (messageText != null) messageText.text = "";
            if (messagePanel != null && messagePanel.activeInHierarchy && !isPersistentMessageActive)
                messagePanel.SetActive(false);
        }
        catch { /* defensive */ }
    }

    // ----------------------------------------
    // Convenience helpers used by your other scripts
    // ----------------------------------------

    /// <summary>
    /// Show a short instant message (no typing) and hide after messageStayTime.
    /// Non-blocking: returns immediately.
    /// </summary>
    public void ShowMessageThenHideInstant(string text)
    {
        // Stop previous helper coroutine to avoid overlap
        StopActiveRoutine();
        activeRoutine = StartCoroutine(ShowMessageThenHideInstantCoroutine(text));
    }

    /// <summary>
    /// Coroutine variant: show instantly and wait then hide.
    /// Callers can StartCoroutine(...) on this if they want to wait.
    /// </summary>
    public IEnumerator ShowMessageThenHideInstantCoroutine(string text)
{
    ShowMessageInstant(text);

    // Use unscaled wait
    yield return new WaitForSecondsRealtime(messageStayTime);

    if (!isPersistentMessageActive)
    {
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
            if (messageText != null) messageText.text = "";
        }
    }

    activeRoutine = null;
}

    /// <summary>
    /// Immediately hide panel and stop any running helpers (compatibility method).
    /// </summary>
    public void HideInstant()
    {
        StopActiveRoutine();

        if (messageText != null) messageText.text = "";
        if (messagePanel != null && messagePanel.activeInHierarchy) messagePanel.SetActive(false);

        // Clear persistent flag too
        isPersistentMessageActive = false;
    }
    /// <summary>
/// Typewriter-style message that DOES NOT auto-hide at the end.
/// Caller is responsible for HideInstant() or ClearPersistentMessage() afterwards.
/// </summary>
public IEnumerator ShowMessagePersistent(string message)
{
    // Stop previous routines
    StopActiveRoutine();

    if (messagePanel == null || messageText == null)
    {
        Debug.LogWarning("⚠️ BattleMessageUI missing references!");
        yield break;
    }

    if (!messagePanel.activeInHierarchy) messagePanel.SetActive(true);

    // mark persistent so TypeTextCoroutine won't auto-hide at the end
    isPersistentMessageActive = true;

    // type text
    typingCoroutine = StartCoroutine(TypeTextCoroutine(message));
    // But TypeTextCoroutine currently hides at the end when !isPersistentMessageActive.
    // Because we set isPersistentMessageActive = true, it will not auto-hide.
    activeRoutine = typingCoroutine;

    yield return typingCoroutine;

    // don't clear isPersistentMessageActive here — caller will call HideInstant() or ClearPersistentMessage()
    typingCoroutine = null;
    activeRoutine = null;
}

}
