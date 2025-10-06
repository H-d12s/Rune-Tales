using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CutsceneForest : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public Image ForestImage1;
    public Image ForestImage2;
    public Image ForestImage3;

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
    yield return StartCoroutine(ShowScene(ForestImage1, new string[]
    {
        "The heroes stepped into the Enchanted Forest, where every tree pulsed with ancient runes.",
        "Whispers of forgotten spells drifted through the air, guiding their path deeper inside.",
        "They knew the Runic Dragon’s power lingered here, watching their every move."
    }));

    // Scene 2
    yield return StartCoroutine(ShowScene(ForestImage2, new string[]
    {
        "Glittering fairies danced between branches, their light mingling with the glow of runes.",
        "Strange animals of myth — antlered wolves and feathered stags — watched from the shadows.",
        "The forest seemed alive, testing whether the heroes were friend… or foe."
    }));

    // Scene 3
    yield return StartCoroutine(ShowScene(ForestImage3, new string[]
    {
        "Beyond the trees, a dark silhouette pierced the horizon — an ominous castle of stone and rune.",
        "Its towers pulsed faintly with crimson light, as though feeding on the forest itself.",
        "The heroes pressed on, their fate bound to the secrets within those walls."
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

