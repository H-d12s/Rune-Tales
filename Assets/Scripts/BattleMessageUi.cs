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
    public float messageStayTime = 1.0f;  // Time before message disappears

    private Coroutine currentRoutine;

    private void Awake()
    {
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }

    /// <summary>
    /// Displays a message with a typewriter effect. Yield this in coroutines.
    /// </summary>
    public IEnumerator ShowMessage(string message)
    {
        if (messagePanel == null || messageText == null)
        {
            Debug.LogWarning("⚠️ BattleMessageUI is missing references!");
            yield break;
        }

        // Stop previous typing if needed
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        messagePanel.SetActive(true);
        messageText.text = "";

        foreach (char c in message)
        {
            messageText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        // Wait before hiding
        yield return new WaitForSeconds(messageStayTime);
        messagePanel.SetActive(false);
    }

    /// <summary>
    /// Displays message instantly (without typing animation).
    /// </summary>
    public void ShowMessageInstant(string message)
    {
        if (messagePanel == null || messageText == null)
            return;

        messagePanel.SetActive(true);
        messageText.text = message;
    }
}
