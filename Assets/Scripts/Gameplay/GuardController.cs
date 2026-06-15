using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D guard AI for the Garden exploration scene.
///
/// States:
///   Patrol  → walks between waypoints, idle when waiting.
///   Chase   → full-speed pursuit when guard can directly see/hear the player.
///   Alerted → moves to last-known position at reduced speed when direct contact
///             is lost; also entered when alerted by the GuardAlertNetwork.
///
/// Guard communication:
///   When a guard enters Chase it broadcasts via GuardAlertNetwork so nearby
///   guards that can't directly sense the player move toward the last-known
///   position at alertedSpeed.
///
/// Animation:
///   Requires an Animator with float "Speed" and bool "Alerted" parameters.
///   Speed drives Idle/Walk/Run blend; Alerted triggers a look-around pose.
///
/// Ground placement:
///   On Start the guard snaps to the ground via a downward raycast so it
///   never floats above terrain.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GuardController : MonoBehaviour
{
    public enum GuardState { Patrol, Chase, Alerted }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Patrol")]
    public List<Transform> waypoints = new List<Transform>();
    public float moveSpeed         = 2f;
    public float waypointTolerance = 1.2f;
    public float waypointWaitTime  = 1.5f;

    [Header("Chase")]
    public float chaseSpeed    = 4f;
    public float catchDistance = 1.6f;

    [Header("Alerted (lost sight / network alert)")]
    [Tooltip("Speed when moving toward last-known position without direct contact.")]
    public float alertedSpeed  = 2.8f;
    [Tooltip("Seconds guard investigates last-known position before returning to patrol.")]
    public float alertDuration = 5f;

    [Header("Vision")]
    public float     visionRange = 14f;
    [Tooltip("Half-angle of forward vision cone in degrees.")]
    public float     visionAngle = 50f;
    [Tooltip("Eye height offset above transform.position used for line-of-sight raycasts.")]
    public float     eyeHeight   = 1.6f;
    public LayerMask obstacleMask;

    [Header("Hearing")]
    [Tooltip("Base hearing radius at full noise (NoiseLevel = 1).")]
    public float hearingRadius = 6f;

    [Header("Animation")]
    [Tooltip("Animator on the guard mesh. Needs float 'Speed' and bool 'Alerted'.")]
    public Animator guardAnimator;

    // ── State ─────────────────────────────────────────────────────────────────

    public GuardState State { get; private set; } = GuardState.Patrol;

    // ── Private ───────────────────────────────────────────────────────────────

    private CharacterController     _cc;
    private Transform               _player;
    private PlayerStealthController _playerCtrl;

    private int   _waypointIndex;
    private bool  _waiting;
    private float _alertTimer;
    private bool  _hasCaught;

    private Vector3 _lastKnownPos;
    private bool    _hasLastKnown;

    private const float Gravity = -15f;
    private float _vertVel;
    // Grace period (seconds) before guards start sensing, to prevent
    // false catches during scene-load frame spikes.
    private float _senseDelay = 2f;
    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        var pg = GameObject.FindGameObjectWithTag("Player");
        if (pg != null)
        {
            _player     = pg.transform;
            _playerCtrl = pg.GetComponent<PlayerStealthController>();
        }
        if (guardAnimator == null)
            guardAnimator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        SnapToGround();
        GuardAlertNetwork.Register(this);
    }

    void OnDestroy()
    {
        GuardAlertNetwork.Unregister(this);
    }

    void Update()
    {
        if (_hasCaught) return;
        ApplyGravity();

        if (_senseDelay > 0f)
        {
            _senseDelay -= Time.deltaTime;
            UpdatePatrol();
            UpdateAnimation();
            return;
        }

        SensePlayer();

        switch (State)
        {
            case GuardState.Patrol:  UpdatePatrol();  break;
            case GuardState.Chase:   UpdateChase();   break;
            case GuardState.Alerted: UpdateAlerted(); break;
        }

        UpdateAnimation();
    }

    // ── Ground snapping ───────────────────────────────────────────────────────

    private void SnapToGround()
    {
        // Raycast downward from slightly above to find the ground surface
        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Disable the CC temporarily (it fights with teleportation)
            _cc.enabled = false;
            transform.position = hit.point;
            _cc.enabled = true;
        }
    }

    // ── Gravity ───────────────────────────────────────────────────────────────

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _vertVel < 0f) _vertVel = -2f;
        _vertVel += Gravity * Time.deltaTime;
    }

    // ── Animation ────────────────────────────────────────────────────────────

    private void UpdateAnimation()
    {
        if (guardAnimator == null) return;

        float speed = 0f;
        switch (State)
        {
            case GuardState.Patrol:
                speed = _waiting ? 0f : moveSpeed;
                break;
            case GuardState.Chase:
                speed = chaseSpeed;
                break;
            case GuardState.Alerted:
                speed = alertedSpeed;
                break;
        }

        guardAnimator.SetFloat("Speed",   speed);
        guardAnimator.SetBool ("Alerted", State == GuardState.Alerted);
    }

    // ── Sensing ───────────────────────────────────────────────────────────────

    private void SensePlayer()
    {
        if (_player == null) return;
        if (CanSeePlayer() || CanHearPlayer())
        {
            _lastKnownPos = _player.position;
            _hasLastKnown = true;
            EnterChase();
        }
    }

    private bool CanSeePlayer()
    {
        if (_player == null) return false;
        Vector3 eye      = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = (_player.position + Vector3.up * 0.9f) - eye;
        float   dist     = toPlayer.magnitude;
        if (dist > visionRange) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > visionAngle) return false;
        return !Physics.Raycast(eye, toPlayer.normalized, dist, obstacleMask);
    }

    private bool CanHearPlayer()
    {
        if (_player == null) return false;
        float noise  = _playerCtrl != null ? _playerCtrl.NoiseLevel : 1f;
        float radius = hearingRadius * noise;
        return radius > 0.05f &&
               Vector3.Distance(transform.position, _player.position) <= radius;
    }

    // ── Patrol ────────────────────────────────────────────────────────────────

    private void UpdatePatrol()
    {
        if (waypoints.Count == 0 || _waiting) return;
        var target = waypoints[_waypointIndex].position;
        MoveToward(target, moveSpeed);

        float flat = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(target.x, target.z));
        if (flat < waypointTolerance)
            StartCoroutine(WaypointWait());
    }

    private IEnumerator WaypointWait()
    {
        _waiting = true;
        yield return new WaitForSeconds(waypointWaitTime);
        _waypointIndex = (_waypointIndex + 1) % Mathf.Max(1, waypoints.Count);
        _waiting = false;
    }

    // ── Chase ─────────────────────────────────────────────────────────────────

    private void EnterChase()
    {
        State = GuardState.Chase;
        _alertTimer = alertDuration;
        // Broadcast to all other guards
        GuardAlertNetwork.BroadcastAlert(this, _lastKnownPos);
    }

    private void UpdateChase()
    {
        if (_player == null) return;

        bool canSee  = CanSeePlayer();
        bool canHear = CanHearPlayer();

        if (canSee || canHear)
        {
            _lastKnownPos = _player.position;
            _alertTimer   = alertDuration;
            MoveToward(_player.position, chaseSpeed);
        }
        else
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f)
            {
                EnterAlerted(_lastKnownPos);
                return;
            }
            // Still heading to last-known while timer ticks
            MoveToward(_lastKnownPos, alertedSpeed);
        }

        if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
        {
            _hasCaught = true;
            ExplorationSceneManager.Instance?.OnPlayerCaught();
        }
    }

    // ── Alerted ───────────────────────────────────────────────────────────────

    /// <summary>Called locally when sight is lost, or remotely by GuardAlertNetwork.</summary>
    public void EnterAlerted(Vector3 targetPos)
    {
        if (State == GuardState.Chase) return;   // already chasing, ignore network alert
        _lastKnownPos = targetPos;
        _hasLastKnown = true;
        State         = GuardState.Alerted;
        _alertTimer   = alertDuration;
    }

    private void UpdateAlerted()
    {
        if (!_hasLastKnown) { State = GuardState.Patrol; return; }

        MoveToward(_lastKnownPos, alertedSpeed);
        _alertTimer -= Time.deltaTime;

        float dist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(_lastKnownPos.x, _lastKnownPos.z));

        if (dist < waypointTolerance || _alertTimer <= 0f)
        {
            _hasLastKnown = false;
            State         = GuardState.Patrol;
        }
    }

    // ── Movement helper ───────────────────────────────────────────────────────

    private void MoveToward(Vector3 target, float speed)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.magnitude > 0.05f)
        {
            dir.Normalize();
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }
        _cc.Move((dir * speed + Vector3.up * _vertVel) * Time.deltaTime);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public void ResetGuard()
    {
        _hasCaught     = false;
        _waiting       = false;
        _alertTimer    = 0f;
        _waypointIndex = 0;
        _hasLastKnown  = false;
        State          = GuardState.Patrol;
        StopAllCoroutines();
        if (guardAnimator != null)
        {
            guardAnimator.SetFloat("Speed",   0f);
            guardAnimator.SetBool ("Alerted", false);
        }
    }
}
