using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// LevelUpUI that uses CanvasGroup transparency instead of enabling/disabling the GameObject.
/// Coroutines run even while visually hidden because the component/gameobject stays active.
/// Uses unscaled time so UI works when Time.timeScale == 0.
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    [Header("UI References (assign in inspector)")]
    public GameObject panel;         // root panel to toggle alpha on (should have a CanvasGroup)
    public TMP_Text titleText;       // headline (e.g. "Level Up!")
    public TMP_Text bodyText;        // message body
    public Button continueButton;    // "OK" / Continue button

    [Header("Defaults")]
    public float defaultAutoHideSeconds = 1.2f;      // non-blocking message display time
    public float minStaggerBetweenMessages = 0.06f;  // small gap when showing message sequences
    public float fadeDuration = 0.18f;              // fade in/out duration (unscaled)

    // internal
    private CanvasGroup canvasGroup;
    private bool waitingForContinue = false;
    private Coroutine activeSequence = null;

    private void Awake()
    {
        // sanity checks
        if (panel == null)
        {
            Debug.LogWarning("[LevelUpUI] panel not assigned - disabling this component.");
            enabled = false;
            return;
        }

        // Ensure there's a CanvasGroup for alpha control
        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        // Start hidden but the GameObject and component remain active so coroutines run
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (titleText != null) titleText.text = "";
        if (bodyText != null) bodyText.text = "";

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
        }

        // Make sure the panel GameObject itself is active so CanvasGroup works and coroutines run.
        // The actual visibility is controlled via canvasGroup.alpha.
        if (!panel.activeInHierarchy) panel.SetActive(true);
    }

    // Simple fade helpers (unscaled)
    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
            yield break;

        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeIn()
    {
        // make visible and interactive at the end of fade in
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        yield return StartCoroutine(FadeTo(1f, fadeDuration));
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private IEnumerator FadeOut()
    {
        // disable interaction immediately to avoid input while fading out
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        yield return StartCoroutine(FadeTo(0f, fadeDuration));
    }

    /// <summary>
    /// Show a blocking popup: player must press Continue or it times out after optional timeoutSeconds (0 = no timeout).
    /// Use StartCoroutine(LevelUpUI.ShowBlocking(...)).
    /// </summary>
    public IEnumerator ShowBlocking(string title, string message, float timeoutSeconds = 0f)
    {
        if (panel == null || bodyText == null)
        {
            Debug.LogWarning("[LevelUpUI] ShowBlocking called but panel/bodyText not assigned.");
            yield break;
        }

        if (titleText != null) titleText.text = title ?? "";
        bodyText.text = message ?? "";

        // Fade in
        yield return StartCoroutine(FadeIn());

        waitingForContinue = true;

        // attach listener with local closure so we can remove it safely
        UnityEngine.Events.UnityAction onClick = null;
        if (continueButton != null)
        {
            onClick = () =>
            {
                waitingForContinue = false;
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

        // cleanup listener
        if (continueButton != null && onClick != null)
        {
            try { continueButton.onClick.RemoveListener(onClick); } catch { }
        }

        // Fade out and clear
        yield return StartCoroutine(FadeOut());
        bodyText.text = "";
        if (titleText != null) titleText.text = "";
    }

    /// <summary>
    /// Show a non-blocking (auto-hide) message for duration seconds. Use StartCoroutine(...).
    /// </summary>
    public IEnumerator ShowNonBlocking(string title, string message, float durationSeconds = -1f)
    {
        if (panel == null || bodyText == null)
        {
            Debug.LogWarning("[LevelUpUI] ShowNonBlocking called but panel/bodyText not assigned.");
            yield break;
        }

        if (durationSeconds <= 0f) durationSeconds = defaultAutoHideSeconds;

        if (titleText != null) titleText.text = title ?? "";
        bodyText.text = message ?? "";

        // Fade in
        yield return StartCoroutine(FadeIn());

        // Wait using unscaled time
        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out and clear
        yield return StartCoroutine(FadeOut());

        bodyText.text = "";
        if (titleText != null) titleText.text = "";
    }

    /// <summary>
    /// Show a sequence of non-blocking messages (one after another).
    /// </summary>
    public IEnumerator ShowSequenceNonBlocking(List<string> messages, float perMessageDuration = -1f)
    {
        if (messages == null || messages.Count == 0) yield break;

        // Only allow one active sequence at a time; if one is running, wait for it to finish
        if (activeSequence != null)
        {
            yield return activeSequence;
        }

        activeSequence = StartCoroutine(RunSequenceNonBlocking(messages, perMessageDuration));
        yield return activeSequence;
        activeSequence = null;
    }

    private IEnumerator RunSequenceNonBlocking(List<string> messages, float perMessageDuration)
    {
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
