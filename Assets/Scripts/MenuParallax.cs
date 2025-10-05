using UnityEngine;
using UnityEngine.InputSystem; // <-- Required for new input system

public class MenuParallax : MonoBehaviour
{
    public float offsetMultiplier = 1f;
    public float smoothTime = .3f;

    private Vector2 startPosition;
    private Vector3 velocity;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // Use Mouse.current for new input system
        if (Mouse.current != null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector2 offset = Camera.main.ScreenToViewportPoint(mousePosition);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                startPosition + (offset * offsetMultiplier),
                ref velocity,
                smoothTime
            );
        }
    }
}
