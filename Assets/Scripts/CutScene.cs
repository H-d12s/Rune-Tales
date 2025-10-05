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

    private MurfTTSStream murfTTS;
    private AudioSource audioSource;
    private int turnCounter = 0; // Context ID counter

    private void Start()
    {
        // Obtain or add MurfTTSStream component
        murfTTS = GetComponent<MurfTTSStream>();
        if (!murfTTS)
            murfTTS = gameObject.AddComponent<MurfTTSStream>();

        // Obtain or add AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Start the cinematic sequence
        StartCoroutine(PlayFullCutscene());
    }

    IEnumerator PlayFullCutscene()
    {
        // --- SCENE 1: The Dragon ---
        yield return StartCoroutine(ShowScene(dragonImage, new string[]
        {
            "Long ago, when the skies still shimmered with magic, there lived the Runic Dragon — keeper of balance and master of the sacred runes.",
            "Every ten years, he would open the gates of his celestial fortress and scatter his runes across the lands.",
            "These runes brought life, fortune, and wisdom to humankind… a gift from the eternal guardian himself."
        }));

        // --- SCENE 2: The Rune Plague ---
        yield return StartCoroutine(CrossfadeImages(dragonImage, plagueImage));
        yield return StartCoroutine(ShowScene(plagueImage, new string[]
        {
            "But greed took root in the hearts of men.",
            "The blessed runes that once healed and strengthened were stolen, twisted for power and war.",
            "Betrayed and broken, the Runic Dragon’s wrath tore across the realm — his sorrow birthing the Rune Plague.",
            "The gifts of old became curses. And the land was forever changed."
        }));

        // --- SCENE 3: The Town of Eldhaven ---
        yield return StartCoroutine(CrossfadeImages(plagueImage, townImage));
        yield return StartCoroutine(ShowScene(townImage, new string[]
        {
            "Centuries have passed since that day.",
            "The once-prosperous kingdoms now cling to survival, their hopes fading with each generation.",
            "Yet from the quiet town of Eldhaven, a new expedition is forming…",
            "Warriors, mages, and wanderers all seeking the same impossible goal —",
            "To reach the dragon’s fortress and end the Rune Plague once and for all."
        }));

        // Optional: load next scene
        // SceneManager.LoadScene("CharacterCreation");
    }

    IEnumerator ShowScene(Image sceneImage, string[] lines)
    {
        foreach (string line in lines)
        {
            yield return StartCoroutine(ShowLine(line));
        }
    }

    IEnumerator ShowLine(string text)
    {
        dialogueText.text = text;
        yield return StartCoroutine(FadeText(0, 1));

        // Generate unique context ID for each narration line
        string contextId = $"cutscene_turn_{++turnCounter}";

        if (murfTTS != null)
        {
            murfTTS.SendTurn(contextId, text);

            // Wait while the audio plays
            while (audioSource.isPlaying)
                yield return null;
        }
        else
        {
            // Fallback pause
            yield return new WaitForSeconds(holdDuration);
        }

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

    IEnumerator CrossfadeImages(Image fromImage, Image toImage)
    {
        float t = 0;
        Color fromColor = fromImage.color;
        Color toColor = toImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float blend = t / fadeDuration;

            fromImage.color = new Color(fromColor.r, fromColor.g, fromColor.b, Mathf.Lerp(1, 0, blend));
            toImage.color = new Color(toColor.r, toColor.g, toColor.b, Mathf.Lerp(0, 1, blend));

            yield return null;
        }

        fromImage.color = new Color(fromColor.r, fromColor.g, fromColor.b, 0);
        toImage.color = new Color(toColor.r, toColor.g, toColor.b, 1);
    }
}
