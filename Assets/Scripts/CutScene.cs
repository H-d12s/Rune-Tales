using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class Cutscene : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public Image dragonImage;
    public Image plagueImage;
    public Image townImage;

    [Header("Timings")]
    public float fadeDuration = 1f;
    public float holdDuration = 3f;
    public float initialFadeDuration = 6f; // 6 seconds for initial fade-in

    private MurfTTSStream murfTTS;
    private AudioSource audioSource;
    private int turnCounter = 0;

    private bool isFirstImageFade = true; // Track first fade

    private void Start()
    {
        murfTTS = GetComponent<MurfTTSStream>();
        if (!murfTTS)
            murfTTS = gameObject.AddComponent<MurfTTSStream>();

        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        StartCoroutine(PlayFullCutscene());
    }

    IEnumerator PlayFullCutscene()
    {
        // Scene 1 - initial image with long fade
        yield return StartCoroutine(ShowScene(dragonImage, new string[]
        {
           "Long ago, when the skies still shimmered with magic, there lived the Runic Dragon — keeper of balance and master of the sacred runes.",
            "Every ten years, he would open the gates of his celestial fortress and scatter his runes across the lands.",
            "These runes brought life, fortune, and wisdom to humankind… a gift from the eternal guardian himself."
        }));

        // Following scenes with normal fade time
        
        yield return StartCoroutine(ShowScene(plagueImage, new string[]
        {
            "But greed took root in the hearts of men.",
            "The blessed runes that once healed and strengthened were stolen, twisted for power and war.",
            "Betrayed and broken, the Runic Dragon’s wrath tore across the realm — his sorrow birthing the Rune Plague.",
            "The gifts of old became curses. And the land was forever changed."
        }));

        
        yield return StartCoroutine(ShowScene(townImage, new string[]
        {
            "Centuries have passed since that day.",
            "The once-prosperous kingdoms now cling to survival, their hopes fading with each generation.",
            "Yet from the quiet town of Eldhaven, a new expedition is forming…",
            "Warriors, mages, and wanderers all seeking the same impossible goal —",
            "To reach the dragon’s fortress and end the Rune Plague once and for all."
        }));

        // Optional next scene
        // SceneManager.LoadScene("CharacterCreation");
    }

    IEnumerator ShowScene(Image sceneImage, string[] lines)
    {
        // Fade in the scene image with initial longer fade for first image only
        float fadeTime = isFirstImageFade ? initialFadeDuration : fadeDuration;
        isFirstImageFade = false; // Subsequent images fade normally

        // Set initial alpha to 0 and enable image
        sceneImage.color = new Color(sceneImage.color.r, sceneImage.color.g, sceneImage.color.b, 0);
        sceneImage.gameObject.SetActive(true);

        // Fade in the image over fadeTime
        float t = 0;
        Color c = sceneImage.color;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0, 1, t / fadeTime);
            sceneImage.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }
        sceneImage.color = new Color(c.r, c.g, c.b, 1);

        // Show all lines in the scene
        foreach (string line in lines)
        {
            yield return StartCoroutine(ShowLine(line));
        }
    }

    IEnumerator ShowLine(string text)
{
    string contextId = $"cutscene_turn_{++turnCounter}";

    if (murfTTS != null)
    {
        // Start streaming TTS audio
        murfTTS.SendTurn(contextId, text);

        // Wait until audio starts playing (may take some time to buffer)
        while (!audioSource.isPlaying)
            yield return null;

        // Now display text and fade in
        dialogueText.text = text;
        yield return StartCoroutine(FadeText(0, 1));

        // Hold text while audio is playing
        while (audioSource.isPlaying)
            yield return null;
    }
    else
    {
        // fallback without TTS
        dialogueText.text = text;
        yield return StartCoroutine(FadeText(0, 1));
        yield return new WaitForSeconds(holdDuration);
    }

    // Fade out text only after audio ended
    yield return StartCoroutine(FadeText(1, 0));
}

    IEnumerator FadeText(float startAlpha, float endAlpha)
    {
        float t = 0;
        Color c = dialogueText.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t / fadeDuration);
            dialogueText.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        dialogueText.color = new Color(c.r, c.g, c.b, endAlpha);
    }

    
}

