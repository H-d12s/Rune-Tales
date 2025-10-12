using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single healthbar UI instance. Shows/hides XP bar for enemies.
/// Provides smooth animations for HP and XP changes.
/// </summary>
public class HealthbarController : MonoBehaviour
{
    [Header("UI refs")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public Image hpFill;        // Image.type = Filled, Fill Method = Horizontal
    public Image xpFill;        // thin bar under HP (optional)
    public GameObject xpContainer; // parent GameObject for xpFill so it can be toggled

    [Header("Animation")]
    public float hpAnimDuration = 0.45f;
    public float xpAnimDuration = 0.5f;

    // internal state
    private Coroutine hpAnim;
    private Coroutine xpAnim;

    // runtime binding
    private CharacterRuntime boundRuntime;

    /// <summary>
    /// Set basic text/visibility. Call after instantiating.
    /// </summary>
    public void Init(string displayName, int level, bool isPlayerBar)
    {
        if (nameText != null)
            nameText.text = displayName + (level > 0 ? $" (Lv{level})" : "");
        if (levelText != null)
            levelText.text = $"Lv {level}";

        // XP only for player bars (enemies typically don't have xp)
        if (xpContainer != null)
            xpContainer.SetActive(isPlayerBar);

        // default fill amounts (full)
        if (hpFill != null) hpFill.fillAmount = 1f;
        if (xpFill != null) xpFill.fillAmount = 0f;
    }

    /// <summary>
    /// Bind a CharacterRuntime to this healthbar so the bar can read initial values.
    /// This does NOT subscribe to runtime changes automatically (safe).
    /// If you want automatic updates when XP changes, we can add an event subscription.
    /// </summary>
    public void BindRuntime(CharacterRuntime runtime)
    {
        boundRuntime = runtime;
        if (boundRuntime == null) return;

        // Update displayed texts if available
        if (nameText != null)
            nameText.text = boundRuntime.baseData.characterName + (boundRuntime.currentLevel > 0 ? $" (Lv{boundRuntime.currentLevel})" : "");
        if (levelText != null)
            levelText.text = $"Lv {boundRuntime.currentLevel}";

        // Set initial HP fill from runtime
        if (hpFill != null && boundRuntime.runtimeHP > 0)
        {
            float percent = (float)boundRuntime.currentHP / Mathf.Max(1, boundRuntime.runtimeHP);
            hpFill.fillAmount = Mathf.Clamp01(percent);
        }

        // If an ExperienceSystem exists in scene, set the XP fill to stored XP percentage (if xpContainer is active)
        var exp = FindObjectOfType<ExperienceSystem>();
        if (xpFill != null && xpContainer != null && xpContainer.activeSelf && exp != null)
        {
            try
            {
                int storedXP = exp.GetStoredXPFor(boundRuntime.baseData.characterName);
                int xpToNext = exp.GetXPToNextLevel(boundRuntime.currentLevel);
                float xpPercent = xpToNext > 0 ? Mathf.Clamp01((float)storedXP / xpToNext) : 0f;
                xpFill.fillAmount = xpPercent;
            }
            catch
            {
                // If ExperienceSystem in your current project doesn't provide those methods/signatures,
                // we fail quietly to avoid compile/runtime exceptions.
            }
        }
    }

    /// <summary>
    /// Immediately set HP (no animation).
    /// percent: 0..1
    /// </summary>
    public void SetHPInstant(float percent)
    {
        if (hpAnim != null) StopCoroutine(hpAnim);
        if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(percent);
    }

    /// <summary>
    /// Animate HP to target percent.
    /// </summary>
    public void AnimateHP(float targetPercent)
    {
        if (hpAnim != null) StopCoroutine(hpAnim);
        hpAnim = StartCoroutine(AnimateFillRoutine(hpFill, targetPercent, hpAnimDuration));
    }

    /// <summary>
    /// Animate XP bar (if present) to target percent.
    /// </summary>
    public void AnimateXP(float targetPercent)
    {
        if (xpFill == null || xpContainer == null || !xpContainer.activeSelf) return;
        if (xpAnim != null) StopCoroutine(xpAnim);
        xpAnim = StartCoroutine(AnimateFillRoutine(xpFill, targetPercent, xpAnimDuration));
    }

    /// <summary>
    /// Combined damage animation helper.
    /// If useXPFirst && xp bar is active: animate xp from current -> a value (often used for visual damage ramp),
    /// then animate actual HP (hpFill). For enemies you will call with useXPFirst = false.
    /// </summary>
    public IEnumerator AnimateDamageSequence(float finalHpPercent, bool useXPFirst = true, float xpInterimPercent = -1f)
    {
        // Clamp
        finalHpPercent = Mathf.Clamp01(finalHpPercent);

        // If we have xp bar and want to use it as an interim visual, animate it first.
        if (useXPFirst && xpContainer != null && xpContainer.activeSelf && xpFill != null)
        {
            // if xpInterimPercent < 0, just animate xp to finalHpPercent
            float xpTarget = (xpInterimPercent >= 0f) ? Mathf.Clamp01(xpInterimPercent) : finalHpPercent;
            yield return AnimateFillRoutineCoroutine(xpFill, xpTarget, xpAnimDuration);
            // small pause
            yield return new WaitForSeconds(0.08f);
        }

        // Animate HP
        yield return AnimateFillRoutineCoroutine(hpFill, finalHpPercent, hpAnimDuration);

        // Optionally reset xp fill (so xp bar doesn't remain showing damage)
        if (xpContainer != null && xpContainer.activeSelf && xpFill != null)
        {
            // reset xp to 0 or match hp
            yield return AnimateFillRoutineCoroutine(xpFill, 0f, 0.15f);
        }
    }

    // --- Utility animators ---
    private IEnumerator AnimateFillRoutine(Image img, float target, float duration)
    {
        if (img == null) yield break;
        float start = img.fillAmount;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            img.fillAmount = Mathf.Lerp(start, Mathf.Clamp01(target), t / duration);
            yield return null;
        }
        img.fillAmount = Mathf.Clamp01(target);
    }

    // Public wrapper so coroutine can be yielded by caller
    private IEnumerator AnimateFillRoutineCoroutine(Image img, float target, float duration)
    {
        yield return AnimateFillRoutine(img, target, duration);
    }
}
