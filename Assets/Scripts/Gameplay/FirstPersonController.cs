using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person character controller for the Garden exploration scene.
/// Requires a CharacterController on the same GameObject.
/// Assign cameraTransform to a child Camera positioned at eye height.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed        = 4f;
    public float sprintMultiplier = 1.8f;
    public float gravity          = -15f;

    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Look")]
    public Transform cameraTransform;
    public float     mouseSensitivity = 0.12f;
    public float     maxLookAngle     = 80f;

    private CharacterController _cc;
    private float _verticalVelocity;
    private float _cameraPitch;
    private bool  _controlEnabled = true;

    public bool  IsGrounded      => _cc != null && _cc.isGrounded;
    /// <summary>Current vertical velocity — positive when jumping, negative when falling.</summary>
    public float VerticalVelocity => _verticalVelocity;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        LockCursor(true);
    }

    void Update()
    {
        if (!_controlEnabled) return;
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var delta = mouse.delta.ReadValue();
        transform.Rotate(0f, delta.x * mouseSensitivity, 0f);
        _cameraPitch -= delta.y * mouseSensitivity;
        _cameraPitch  = Mathf.Clamp(_cameraPitch, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = 0f, z = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    z =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  z = -1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x =  1f;

        bool  sprinting  = kb.leftShiftKey.isPressed;
        float speed      = moveSpeed * (sprinting ? sprintMultiplier : 1f);

        var horizontal = transform.right * x + transform.forward * z;
        if (horizontal.magnitude > 1f) horizontal.Normalize();

        if (_cc.isGrounded)
        {
            if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            if (kb.spaceKey.wasPressedThisFrame) _verticalVelocity = jumpForce;
        }
        _verticalVelocity += gravity * Time.deltaTime;

        _cc.Move((horizontal * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
    }

    /// <summary>Lock or unlock the cursor and toggle control input accordingly.</summary>
    public void LockCursor(bool locked)
    {
        _controlEnabled  = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
