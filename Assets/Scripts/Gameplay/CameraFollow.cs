using UnityEngine;

/// <summary>
/// Smoothly follows a target transform in 2D, clamped to optional world-space bounds.
/// Attach to the Main Camera in gameplay scenes.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("How quickly the camera catches up. Higher = snappier.")]
    public float smoothSpeed = 6f;

    [Tooltip("Half-extents of the world area the camera is clamped to.")]
    public Vector2 boundsHalfExtent = new Vector2(14f, 14f);

    private Camera _cam;

    void Awake() => _cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);

        // Clamp so the camera view does not expose black space outside the map
        if (_cam != null && _cam.orthographic)
        {
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            desired.x = Mathf.Clamp(desired.x, -boundsHalfExtent.x + halfW,  boundsHalfExtent.x - halfW);
            desired.y = Mathf.Clamp(desired.y, -boundsHalfExtent.y + halfH,  boundsHalfExtent.y - halfH);
        }

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
