using System.Collections;
using TMPro;
using UnityEngine;

public class BattleMessageUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject messagePanel;    // Assign your Message Panel GameObject
    public TMP_Text messageText;       // Assign your TMP text component

    [Header("Settings")]
    public float typeSpeed = 0.02f;       // Delay between characters
    public float messageStayTime = 1.0f;  // Time before message disappears

    private Coroutine activeRoutine;

    private void Awake()
    {
        if (messageText != null) messageText.text = "";
        if (messagePanel != null) messagePanel.SetActive(false);
    }

    private void OnDisable()
    {
        // If something disables the panel (or the component), make sure text is cleared and coroutine stopped.
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";
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
    /// Show message with typewriter effect. This IEnumerator is safe to StartCoroutine from any active MonoBehaviour.
    /// </summary>
    public IEnumerator ShowMessage(string message)
    {
        // Stop any previous routine and clear text
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";

        if (messagePanel == null || messageText == null)
        {
            Debug.LogWarning("⚠️ BattleMessageUI missing references!");
            yield break;
        }

        // Ensure the panel GameObject is active BEFORE we proceed
        messagePanel.SetActive(true);

        // Do typing effect inline (no nested StartCoroutine calls)
        foreach (char c in message)
        {
            messageText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        // Keep the full message on screen
        yield return new WaitForSeconds(messageStayTime);

        // Hide and clear text
        messagePanel.SetActive(false);
        messageText.text = "";

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
        messageText.text = message;
    }

    /// <summary>
    /// Immediately hide and clear the message (use when you need to force resume the UI).
    /// </summary>
    public void HideInstant()
    {
        StopActiveRoutine();
        if (messageText != null) messageText.text = "";
        if (messagePanel != null) messagePanel.SetActive(false);
    }
}
