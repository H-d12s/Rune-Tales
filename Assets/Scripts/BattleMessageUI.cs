using System.Collections;
using UnityEngine;
using TMPro;

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
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";

        if (messagePanel == null || messageText == null)
        {
            Debug.LogWarning("⚠️ BattleMessageUI missing references!");
            yield break;
        }

        if (!messagePanel.activeInHierarchy) messagePanel.SetActive(true);

        typingCoroutine = StartCoroutine(TypeTextCoroutine(message));
        activeRoutine = typingCoroutine;

        yield return typingCoroutine;

        typingCoroutine = null;
        activeRoutine = null;
        yield break;
    }

    private IEnumerator TypeTextCoroutine(string message)
    {
        // Typewriter
        if (messageText != null) messageText.text = "";

        foreach (char c in message)
        {
            if (messageText != null) messageText.text += c;
            // use real-time so typing continues if timeScale == 0
            yield return new WaitForSecondsRealtime(typeSpeed);
        }

        // IMPORTANT CHANGE:
        // Only wait the "messageStayTime" when NOT persistent.
        // For persistent messages we want the coroutine to finish immediately after typing,
        // so callers (like the message queue) don't stay blocked waiting for an extra delay.
        if (!isPersistentMessageActive)
        {
            yield return new WaitForSecondsRealtime(messageStayTime);
            if (messagePanel != null) messagePanel.SetActive(false);
            if (messageText != null) messageText.text = "";
        }

        // finished typing
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

    public void ShowPersistentMessage(string text) => SetPersistentMessage(text);

    public void HidePersistentMessage()
    {
        StopActiveRoutine();

        if (messageText != null) messageText.text = "";
        if (messagePanel != null && messagePanel.activeInHierarchy) messagePanel.SetActive(false);

        isPersistentMessageActive = false;
    }

    public void ClearPersistentMessage() => HidePersistentMessage();

    public bool IsPersistentMessageActive() => isPersistentMessageActive;

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

    public void ShowMessageThenHideInstant(string text)
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(ShowMessageThenHideInstantCoroutine(text));
    }

    public IEnumerator ShowMessageThenHideInstantCoroutine(string text)
    {
        ShowMessageInstant(text);
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

    public void HideInstant()
    {
        StopActiveRoutine();

        if (messageText != null) messageText.text = "";
        if (messagePanel != null && messagePanel.activeInHierarchy) messagePanel.SetActive(false);

        isPersistentMessageActive = false;
    }

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

        // type text: we still yield until typing completes, but the final messageStayTime is skipped for persistent messages.
        typingCoroutine = StartCoroutine(TypeTextCoroutine(message));
        activeRoutine = typingCoroutine;

        yield return typingCoroutine;

        typingCoroutine = null;
        activeRoutine = null;
    }

    /// <summary>
    /// Force-stop the typing coroutine immediately (do NOT hide panel).
    /// Useful when a prompt is being replaced/changed and you want to stop typing now.
    /// </summary>
    public void AbortTyping()
    {
        if (typingCoroutine != null)
        {
            try { StopCoroutine(typingCoroutine); } catch { }
            typingCoroutine = null;
        }
        activeRoutine = null;
    }
}
