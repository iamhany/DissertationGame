using UnityEngine;

/// <summary>
/// Companion component on the Player that tracks stealth state.
/// Attach alongside FirstPersonController. Guards read NoiseLevel from this.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerStealthController : MonoBehaviour
{
    [Header("Noise thresholds")]
    [Tooltip("Horizontal speed below which the player is considered silent.")]
    public float silentSpeedThreshold = 0.3f;
    [Tooltip("Horizontal speed at which noise level reaches maximum (sprinting).")]
    public float loudSpeedThreshold   = 7f;

    private CharacterController _cc;

    /// <summary>0 = completely silent, 1 = fully noisy (running). Guards scale hearing radius by this.</summary>
    public float NoiseLevel { get; private set; }

    void Awake() => _cc = GetComponent<CharacterController>();

    void Update()
    {
        var vel   = _cc.velocity;
        float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
        NoiseLevel = Mathf.InverseLerp(silentSpeedThreshold, loudSpeedThreshold, horizontalSpeed);
    }
}
