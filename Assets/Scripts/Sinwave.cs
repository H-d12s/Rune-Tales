using UnityEngine;

public class SinWaveMovement : MonoBehaviour
{
    public float amplitude = 0.5f;   // How high/low it moves
    public float frequency = 2f;     // How fast it oscillates
    public bool horizontal = false;  // Move left/right instead of up/down

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        if (horizontal)
        {
            // Left-right sin movement
            transform.position = startPos + new Vector3(Mathf.Sin(Time.time * frequency) * amplitude, 0f, 0f);
        }
        else
        {
            // Up-down sin movement
            transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * frequency) * amplitude, 0f);
        }
    }
}
