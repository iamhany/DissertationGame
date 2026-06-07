using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Full guard AI for the stealth and escape scenes.
///
/// Vision  – forward cone only (angle + range). Range halved if player is Shifted.
/// Hearing – radius around guard. Zero if player is Shifted.
/// States  – Patrol → Suspicious → Alert → Searching → Patrol
///
/// Alert propagation: when a guard enters Alert it calls NearbyGuards() and
/// broadcasts the player's last known position to all within alertRadius.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GuardController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Vision")]
    [Tooltip("Half-angle of the forward vision cone in degrees.")]
    public float visionAngle   = 40f;
    [Tooltip("Maximum forward vision distance (halved when player is Shifted).")]
    public float visionRange   = 9f;
    [Tooltip("Layer mask for walls that block vision raycasts.")]
    public LayerMask wallMask;

    [Header("Hearing")]
    [Tooltip("Radius in which footstep sound can be heard (0 when player is Shifted).")]
    public float hearingRadius = 5f;

    [Header("Alert")]
    [Tooltip("Radius in which this guard broadcasts an alert to other guards.")]
    public float alertRadius   = 14f;
    [Tooltip("Speed multiplier when chasing player.")]
    public float chaseSpeedMultiplier = 1.4f;

    [Header("Patrol")]
    public List<Transform> waypoints = new List<Transform>();
    public float moveSpeed    = 1.6f;
    public float waypointWaitTime = 1.2f;

    [Header("Detection meter contribution")]
    [Tooltip("How fast detection fills per second when inside vision cone.")]
    public float visionDetectionRate  = 45f;
    [Tooltip("How fast detection fills per second when inside hearing radius.")]
    public float hearingDetectionRate = 20f;

    // ── State machine ─────────────────────────────────────────────────────────

    public enum GuardState { Patrol, Suspicious, Alert, Searching }
    public GuardState State { get; private set; } = GuardState.Patrol;

    // ── Private ───────────────────────────────────────────────────────────────

    private Rigidbody2D    _rb;
    private Transform      _player;
    private PlayerStealthController _playerCtrl;
    private int            _waypointIndex;
    private bool           _waitingAtWaypoint;
    private Vector2        _investigateTarget;
    private float          _searchTimer;
    private const float    SearchDuration = 6f;

    // Colour feedback (uses SpriteRenderer if present)
    private SpriteRenderer _sprite;
    private static readonly Color ColPatrol     = new Color(0.85f, 0.2f, 0.2f);
    private static readonly Color ColSuspicious = new Color(1f,    0.7f, 0f);
    private static readonly Color ColAlert      = new Color(1f,    0f,   0f);
    private static readonly Color ColSearching  = new Color(1f,    0.4f, 0.1f);

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _player     = playerGO.transform;
            _playerCtrl = playerGO.GetComponent<PlayerStealthController>();
        }
    }

    void Update()
    {
        switch (State)
        {
            case GuardState.Patrol:    UpdatePatrol();    break;
            case GuardState.Suspicious: UpdateSuspicious(); break;
            case GuardState.Alert:     UpdateAlert();     break;
            case GuardState.Searching: UpdateSearching(); break;
        }

        CheckSenses();
        UpdateColour();
    }

    // ── State updates ─────────────────────────────────────────────────────────

    private void UpdatePatrol()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        if (_waitingAtWaypoint) return;

        Transform target = waypoints[_waypointIndex];
        MoveToward(target.position, moveSpeed);

        if (Vector2.Distance(transform.position, target.position) < 0.2f)
            StartCoroutine(WaypointWait());
    }

    private void UpdateSuspicious()
    {
        // Move toward last heard/seen position
        MoveToward(_investigateTarget, moveSpeed * 0.7f);

        if (Vector2.Distance(transform.position, _investigateTarget) < 0.5f)
            TransitionTo(GuardState.Patrol);
    }

    private void UpdateAlert()
    {
        if (_player == null) return;
        MoveToward(_player.position, moveSpeed * chaseSpeedMultiplier);
    }

    private void UpdateSearching()
    {
        // Wander around last known position
        MoveToward(_investigateTarget, moveSpeed * 0.8f);
        _searchTimer -= Time.deltaTime;

        if (_searchTimer <= 0f || Vector2.Distance(transform.position, _investigateTarget) < 0.4f)
            TransitionTo(GuardState.Patrol);
    }

    // ── Sense checking ────────────────────────────────────────────────────────

    private void CheckSenses()
    {
        if (_player == null) return;

        bool isShifted = _playerCtrl != null && _playerCtrl.IsShifted;

        if (CanSeePlayer(isShifted))
        {
            float rate = visionDetectionRate;
            StealthSceneManager.Instance?.AddDetection(rate * Time.deltaTime, transform.position);

            if (State != GuardState.Alert)
                BecomeAlert(_player.position);
            return;
        }

        if (!isShifted && CanHearPlayer())
        {
            float rate = hearingDetectionRate;
            StealthSceneManager.Instance?.AddDetection(rate * Time.deltaTime, transform.position);

            if (State == GuardState.Patrol)
                BecomeSuspicious(_player.position);
        }
    }

    private bool CanSeePlayer(bool playerShifted)
    {
        if (_player == null) return false;

        float effectiveRange = playerShifted ? visionRange * 0.35f : visionRange;
        Vector2 toPlayer = (Vector2)_player.position - (Vector2)transform.position;

        if (toPlayer.magnitude > effectiveRange) return false;

        // Must be within forward cone
        float angle = Vector2.Angle((Vector2)transform.up, toPlayer);
        if (angle > visionAngle) return false;

        // Line-of-sight raycast
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, toPlayer.normalized, toPlayer.magnitude, wallMask);
        return hit.collider == null;
    }

    private bool CanHearPlayer()
    {
        if (_player == null) return false;
        float dist = Vector2.Distance(transform.position, _player.position);
        float noise = _playerCtrl != null ? _playerCtrl.NoiseLevel : 1f;
        return dist < hearingRadius * noise;
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private void TransitionTo(GuardState next)
    {
        State = next;
        if (next == GuardState.Searching)
            _searchTimer = SearchDuration;
    }

    private void BecomeAlert(Vector2 playerPos)
    {
        _investigateTarget = playerPos;
        TransitionTo(GuardState.Alert);

        // Broadcast alert to nearby guards
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, alertRadius);
        foreach (var col in nearby)
        {
            var other = col.GetComponent<GuardController>();
            if (other != null && other != this)
                other.ReceiveAlert(playerPos);
        }
    }

    private void BecomeSuspicious(Vector2 soundPos)
    {
        _investigateTarget = soundPos;
        TransitionTo(GuardState.Suspicious);
    }

    /// <summary>Called by a nearby guard that has spotted the player.</summary>
    public void ReceiveAlert(Vector2 playerPos)
    {
        if (State != GuardState.Alert)
            BecomeAlert(playerPos);
    }

    /// <summary>Call when this guard loses visual contact with the player.</summary>
    public void LosePlayer()
    {
        if (State == GuardState.Alert)
        {
            _investigateTarget = _player != null ? (Vector2)_player.position : (Vector2)transform.position;
            TransitionTo(GuardState.Searching);
        }
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void MoveToward(Vector2 target, float speed)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * speed;

        // Rotate to face movement direction (2D: z-axis)
        if (dir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private IEnumerator WaypointWait()
    {
        _waitingAtWaypoint = true;
        _rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(waypointWaitTime);
        _waypointIndex = (_waypointIndex + 1) % waypoints.Count;
        _waitingAtWaypoint = false;
    }

    // ── Visual feedback ───────────────────────────────────────────────────────

    private void UpdateColour()
    {
        if (_sprite == null) return;
        _sprite.color = State switch
        {
            GuardState.Suspicious => ColSuspicious,
            GuardState.Alert      => ColAlert,
            GuardState.Searching  => ColSearching,
            _                     => ColPatrol,
        };
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Vision cone
        Gizmos.color = Color.yellow;
        Vector3 forward = transform.up;
        Vector3 left    = Quaternion.Euler(0f, 0f,  visionAngle) * forward;
        Vector3 right   = Quaternion.Euler(0f, 0f, -visionAngle) * forward;
        Gizmos.DrawLine(transform.position, transform.position + left  * visionRange);
        Gizmos.DrawLine(transform.position, transform.position + right * visionRange);
        Gizmos.DrawLine(transform.position, transform.position + forward * visionRange);

        // Hearing radius
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        // Alert radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, alertRadius);
    }
}
