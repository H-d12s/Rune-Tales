using UnityEngine;

public class BackgroundZoom : MonoBehaviour
{
    public float zoomSpeed = 1f;       // How fast it zooms in/out
    public float zoomAmount = 0.05f;   // Max scale variation (5% bigger/smaller)

    private Vector3 startScale;

    void Start()
    {
        startScale = transform.localScale;
    }

    void Update()
    {
        // PingPong gives a value between 0 and zoomAmount
        float scaleOffset = Mathf.PingPong(Time.time * zoomSpeed, zoomAmount);
        float scaleValue = 1 + scaleOffset;

        transform.localScale = startScale * scaleValue;
    }
}
