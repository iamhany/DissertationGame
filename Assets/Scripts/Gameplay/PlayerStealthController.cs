using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down 2D player controller for the stealth and escape scenes.
///
/// Movement  – WASD / left stick
/// Shift     – Hold Left Shift / South button. Silences footsteps and shrinks
///             the guard's vision cone. Drains a shift meter; recharges when idle.
///
/// NoiseLevel – 0 (silent/shifted) to 1 (running). Guards multiply their
///              hearing radius by this value.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStealthController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Movement")]
    public float walkSpeed   = 3.5f;
    public float shiftedSpeed = 2f;   // slower while shifted (careful steps)

    [Header("Shift (temporal phase)")]
    [Tooltip("Total seconds of shift available.")]
    public float shiftMeterMax      = 5f;
    [Tooltip("Drain rate in seconds per second while active.")]
    public float shiftDrainRate     = 1f;
    [Tooltip("Recharge rate in seconds per second while inactive.")]
    public float shiftRechargeRate  = 0.6f;
    [Tooltip("Minimum charge required before shift can activate again.")]
    public float shiftMinToActivate = 0.5f;

    // ── Public read ───────────────────────────────────────────────────────────

    /// <summary>True while the player is in temporal shift (silent + hard to see).</summary>
    public bool  IsShifted    { get; private set; }

    /// <summary>0 = silent/shifted, 1 = normal footstep. Guards multiply hearing radius by this.</summary>
    public float NoiseLevel   { get; private set; } = 1f;

    /// <summary>0-1 normalised shift meter for the HUD.</summary>
    public float ShiftCharge  => _shiftMeter / shiftMeterMax;

    // ── Private ───────────────────────────────────────────────────────────────

    private Rigidbody2D _rb;
    private Vector2     _moveInput;
    private float       _shiftMeter;

    private SpriteRenderer _sprite;
    private Color _normalColor  = new Color(0.25f, 0.55f, 1f);
    private Color _shiftedColor = new Color(0.7f,  0.9f,  1f, 0.55f);

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _rb          = GetComponent<Rigidbody2D>();
        _sprite      = GetComponent<SpriteRenderer>();
        _shiftMeter  = shiftMeterMax;
    }

    void Update()
    {
        ReadInput();
        UpdateShift();
        UpdateNoise();
        UpdateVisuals();
    }

    // Map bounds — kept generous so the walls are never passed.
    // Matches the boundary wall inner edges in BuildGameplayScene (±16).
    private const float MapHalfX = 16f;
    private const float MapHalfY = 16f;

    void FixedUpdate()
    {
        float speed = IsShifted ? shiftedSpeed : walkSpeed;
        _rb.linearVelocity = _moveInput * speed;

        // Hard clamp — prevents physics tunnelling through boundary walls
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, -MapHalfX, MapHalfX);
        p.y = Mathf.Clamp(p.y, -MapHalfY, MapHalfY);
        transform.position = p;

        if (_moveInput.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(_moveInput.y, _moveInput.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            float h = 0f, v = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
            _moveInput = new Vector2(h, v).normalized;

            bool shiftPressed = kb.leftShiftKey.isPressed || kb.qKey.isPressed;
            TrySetShift(shiftPressed);
        }

        // Gamepad support
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.1f) _moveInput = stick.normalized;
            TrySetShift(gp.buttonSouth.isPressed);
        }
    }

    private void TrySetShift(bool pressed)
    {
        if (pressed && _shiftMeter >= shiftMinToActivate)
            IsShifted = true;
        else if (!pressed)
            IsShifted = false;

        // Can't stay shifted on empty meter
        if (_shiftMeter <= 0f)
            IsShifted = false;
    }

    // ── Shift meter ───────────────────────────────────────────────────────────

    private void UpdateShift()
    {
        if (IsShifted)
        {
            _shiftMeter -= shiftDrainRate * Time.deltaTime;
            _shiftMeter  = Mathf.Max(0f, _shiftMeter);
        }
        else
        {
            _shiftMeter += shiftRechargeRate * Time.deltaTime;
            _shiftMeter  = Mathf.Min(shiftMeterMax, _shiftMeter);
        }
    }

    // ── Noise level ───────────────────────────────────────────────────────────

    private void UpdateNoise()
    {
        if (IsShifted)
        {
            NoiseLevel = 0f;
            return;
        }

        // Moving = 1.0, standing still = 0.15 (guards can still faintly hear)
        NoiseLevel = _moveInput.sqrMagnitude > 0.01f ? 1f : 0.15f;
    }

    // ── Visual feedback ───────────────────────────────────────────────────────

    private void UpdateVisuals()
    {
        if (_sprite == null) return;
        _sprite.color = IsShifted ? _shiftedColor : _normalColor;
    }

    // ── Collision – goal trigger ───────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Exit"))
            StealthSceneManager.Instance?.OnPlayerReachedExit();

        if (other.CompareTag("EscapeExit"))
            EscapeSceneManager.Instance?.OnPlayerEscaped();
    }
}
