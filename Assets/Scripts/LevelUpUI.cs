using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Small dedicated panel used for Level-up and Move-learn notifications.
/// Keeps its own coroutine flow and does not touch BattleMessageUI.
/// Designed to be simple: supports blocking (wait for player press) and non-blocking auto-hide variants.
/// Uses unscaled time so UI works even if Time.timeScale == 0.
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    [Header("UI References (assign in inspector)")]
    public GameObject panel;         // root panel to toggle
    public TMP_Text titleText;       // headline (e.g. "Level Up!")
    public TMP_Text bodyText;        // message body
    public Button continueButton;    // "OK" / Continue button

    [Header("Defaults")]
    public float defaultAutoHideSeconds = 1.2f;      // non-blocking message display time
    public float minStaggerBetweenMessages = 0.06f;  // small gap when showing message sequences

    private bool waitingForContinue = false;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (titleText != null) titleText.text = "";
        if (bodyText != null) bodyText.text = "";
        if (continueButton != null) continueButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Show a blocking popup: player must press Continue or it times out after optional timeoutSeconds (0 = no timeout).
    /// Use StartCoroutine(LevelUpUI.ShowBlocking(...)).
    /// </summary>
    public IEnumerator ShowBlocking(string title, string message, float timeoutSeconds = 0f)
    {
        if (panel == null || bodyText == null)
        {
            Debug.LogWarning("LevelUpUI: missing references for ShowBlocking.");
            yield break;
        }

        panel.SetActive(true);
        if (titleText != null) titleText.text = title ?? "";
        bodyText.text = message ?? "";

        waitingForContinue = true;
        // attach listener
        if (continueButton != null)
        {
            UnityEngine.Events.UnityAction onClick = null;
            onClick = () =>
            {
                waitingForContinue = false;
                try { continueButton.onClick.RemoveListener(onClick); } catch { }
            };
            continueButton.onClick.AddListener(onClick);
        }

        float elapsed = 0f;
        while (waitingForContinue)
        {
            if (timeoutSeconds > 0f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeoutSeconds)
                {
                    waitingForContinue = false;
                    break;
                }
            }
            yield return null;
        }

        // cleanup
        if (continueButton != null)
        {
            try { continueButton.onClick.RemoveAllListeners(); } catch { }
        }

        // hide
        bodyText.text = "";
        if (titleText != null) titleText.text = "";
        panel.SetActive(false);
        yield break;
    }

    /// <summary>
    /// Show a non-blocking (auto-hide) message for duration seconds. Use StartCoroutine(...).
    /// </summary>
    public IEnumerator ShowNonBlocking(string title, string message, float durationSeconds = -1f)
    {
        if (panel == null || bodyText == null)
        {
            Debug.LogWarning("LevelUpUI: missing references for ShowNonBlocking.");
            yield break;
        }

        if (durationSeconds <= 0f) durationSeconds = defaultAutoHideSeconds;

        panel.SetActive(true);
        if (titleText != null) titleText.text = title ?? "";
        bodyText.text = message ?? "";

        // wait realtime
        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // hide
        bodyText.text = "";
        if (titleText != null) titleText.text = "";
        panel.SetActive(false);
        yield break;
    }

    /// <summary>
    /// Show a sequence of non-blocking messages (one after another).
    /// </summary>
    public IEnumerator ShowSequenceNonBlocking(List<string> messages, float perMessageDuration = -1f)
    {
        if (messages == null || messages.Count == 0) yield break;

        foreach (var msg in messages)
        {
            yield return StartCoroutine(ShowNonBlocking("Notification", msg, perMessageDuration));
            // small unscaled gap to avoid immediate overlap
            float elapsed = 0f;
            while (elapsed < minStaggerBetweenMessages)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    /// <summary>
    /// Show a sequence of blocking messages (player must confirm each).
    /// </summary>
    public IEnumerator ShowSequenceBlocking(List<string> messages, float perMessageTimeout = 0f)
    {
        if (messages == null || messages.Count == 0) yield break;

        foreach (var msg in messages)
        {
            yield return StartCoroutine(ShowBlocking("Notification", msg, perMessageTimeout));
        }
    }
}
