using UnityEngine;

/// <summary>
/// Simple pulse scale animation — attached to the exit arrow to draw the player's eye.
/// </summary>
public class ExitPulse : MonoBehaviour
{
    public float pulseSpeed    = 2.2f;
    public float pulseMinScale = 0.7f;
    public float pulseMaxScale = 1.0f;

    private Vector3 _baseScale;

    void Awake() => _baseScale = transform.localScale;

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float s = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
        transform.localScale = _baseScale * s;
    }
}
