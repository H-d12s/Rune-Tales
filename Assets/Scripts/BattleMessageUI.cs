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

    private Coroutine activeRoutine;

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
        // Stop previous typewriter so messages do not clash
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";

        if (messagePanel == null || messageText == null)
        {
            Debug.LogWarning("⚠️ BattleMessageUI missing references!");
            yield break;
        }

        // Ensure panel active
        if (!messagePanel.activeInHierarchy) messagePanel.SetActive(true);

        // Typewriter
        foreach (char c in message)
        {
            messageText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        yield return new WaitForSeconds(messageStayTime);

        // Only auto-hide if not persistent
        if (!isPersistentMessageActive)
        {
            messagePanel.SetActive(false);
            messageText.text = "";
        }

        activeRoutine = null;
        yield break;
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
        // Show instantly (no typing)
        ShowMessageInstant(text);

        // Wait for configured duration (gives player time to read)
        yield return new WaitForSeconds(messageStayTime);

        // Only hide if persistent flag isn't set by something else while we waited
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
}
