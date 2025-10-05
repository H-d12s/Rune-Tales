using UnityEngine;
using TMPro;
using System.Collections;

public class FadeInText : MonoBehaviour
{
    public TMP_Text gameOverText;    // Your TextMeshProUGUI element
    public float fadeInDuration = 2f; // Duration of fade-in

    private void OnEnable()
    {
        gameOverText.alpha = 0f;      // Start invisible
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            gameOverText.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        gameOverText.alpha = 1f; // Ensure fully visible at the end
    }
}
