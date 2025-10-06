using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CutsceneCatacombs : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public Image CatacombsImage1;
    public Image CatacombsImage2;
    public Image CatacombsImage3;

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
    yield return StartCoroutine(ShowScene(CatacombsImage1, new string[]
    {
        "The heroes descended into the Catacombs, a subterranean maze of dark, rough-hewn stone.",
        "The only light came from flickering blue runes, ancient magical torches casting long, unsettling shadows.",
        "They had entered the Runic Dragon's lair, a realm guarded by death and endless suffering."
    }));

    // Scene 2
    yield return StartCoroutine(ShowScene(CatacombsImage2, new string[]
    {
         "Behind every column and corner lay the Dragon's servants: skeletal remnants, bound by dark power.",
        "In rusted prison cells, the heroes saw the horror: immortality granted as a cruel, eternal curse.",
        "These were not merely undead; they were the husks of prisoners, suffering forever for the Dragon's amusement."
    }));

    // Scene 3
    yield return StartCoroutine(ShowScene(CatacombsImage3, new string[]
    {
         "The air shuddered as the skeletons rose from the stone, their hollow eyes glowing with cold, inherited malice.",
        "The enemy had no fear, no pain, and no end to its service, moving with a chilling, mechanical purpose.",
        "There was no path around them; the heroes realized they would have to fight their way through the catacombs' undying guardians."
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

