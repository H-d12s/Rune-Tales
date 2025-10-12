using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CutsceneFinale : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public Image FinalImage1;
    public Image FinalImage2;
    public Image FinalImage3;

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
    // Scene 1
    // Finale Scene 1
yield return StartCoroutine(ShowScene(FinalImage1, new string[]
{
    "At last, the Rune Slayer fell—his armor shattered, his runes dimmed, his long torment ended.",
    "The heroes stood in silence as the ashes of battle settled across the ruined arena.",
    "The ancient dragon’s echo faded into the winds, and with it, the curse that bound the world."
}));

// Finale Scene 2
yield return StartCoroutine(ShowScene(FinalImage2, new string[]
{
    "The Rune Plague began to wane; corrupted sigils crumbled to dust as clean light spread once more.",
    "The runes returned to balance—no longer tools of greed, but whispers of wisdom left behind.",
    "From distant villages to broken towers, life began to stir again beneath gentle dawn light."
}));

// Finale Scene 3
yield return StartCoroutine(ShowScene(FinalImage3, new string[]
{
    "Humanity had learned through sorrow and ruin the price of unchecked desire.",
    "Now they would walk the path of harmony—between magic, nature, and their own hearts.",
    "And as the heroes gazed toward the brightening sky, hope rose with the sun of a new age."
}));

}
    IEnumerator ShowScene(Image sceneImage, string[] lines)
{
    float fadeTime = isFirstImageFade ? initialFadeDuration : fadeDuration;
    isFirstImageFade = false;

    // Set initial alpha to 0
    sceneImage.color = new Color(sceneImage.color.r, sceneImage.color.g, sceneImage.color.b, 0);
    sceneImage.gameObject.SetActive(true);

    // Fade in the image
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

    // Show dialogue lines
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

