using UnityEngine;
using UnityEngine.Rendering.Universal; // Needed for Light2D

public class CandleFlicker : MonoBehaviour
{
    private Light2D light2D;

    [Header("Flicker Settings")]
    public float minIntensity = 0.7f;   // Minimum brightness
    public float maxIntensity = 1.2f;   // Maximum brightness
    public float flickerSpeed = 0.1f;   // Speed of flicker randomness

    private float targetIntensity;

    void Start()
    {
        light2D = GetComponent<Light2D>();
        if (light2D == null)
            Debug.LogError("No Light2D found on this GameObject!");
        
        targetIntensity = light2D.intensity;
    }

    void Update()
    {
        // Randomly change target intensity
        if (Random.value < flickerSpeed)
        {
            targetIntensity = Random.Range(minIntensity, maxIntensity);
        }

        // Smooth transition between intensities
        light2D.intensity = Mathf.Lerp(light2D.intensity, targetIntensity, Time.deltaTime * 8f);
    }
}
