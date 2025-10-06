using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CutsceneVillage : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public Image VillagImage1;
    public Image VillagImage2;
    public Image VillagImage3;

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
    yield return StartCoroutine(ShowScene(VillagImage1, new string[]
    {
        "The heroes, after a long journey found a silent village, shrouded in unnatural mist.",
        "Hollow coughs echoed from shadowed windows; purple veins pulsed beneath pale skin.",
        "This was the Rune Plague—the magic turning against its own people."
    }));

    // Scene 2
    yield return StartCoroutine(ShowScene(VillagImage2, new string[]
    {
        "The runes glowing on the afflicted were erratic and inverted, twisting ancient power into sickness.",
        "Each cursed glyph was a prison, locking the villagers' strength and sanity away.",
        "Examining the symbols, the heroes felt a creeping cold, the raw malice of corrupted magic."
    }));

    // Scene 3
    yield return StartCoroutine(ShowScene(VillagImage3, new string[]
    {
        "The infected would attack any and all living creatures, their eyes burning with madness.",
        "The plague had stolen their minds, leaving only savage aggression and primal instinct.",
        "The path to the forest was blocked; the heroes would have to fight their way through the infected hoard."
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

