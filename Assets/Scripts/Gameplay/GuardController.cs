using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D guard AI for the Garden exploration scene.
///
/// States  : Patrol → Chase → Alerted (lost sight) → Patrol
/// Vision  : forward cone (visionAngle, visionRange). Blocked by obstacleMask.
/// Hearing : radius scaled by PlayerStealthController.NoiseLevel.
/// Catch   : when within catchDistance, notifies ExplorationSceneManager.
/// Reset   : call ResetGuard() to restart from initial state (used on retry).
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
    public float waypointWaitTime  = 1.2f;

    [Header("Chase")]
    public float chaseSpeed    = 4.5f;
    public float catchDistance = 1.5f;

    [Header("Vision")]
    public float     visionRange = 12f;
    [Tooltip("Half-angle of forward vision cone in degrees.")]
    public float     visionAngle = 45f;
    [Tooltip("Eye height offset above transform.position used for line-of-sight raycasts.")]
    public float     eyeHeight   = 1.6f;
    public LayerMask obstacleMask;

    [Header("Hearing")]
    [Tooltip("Base hearing radius at full noise (NoiseLevel = 1).")]
    public float hearingRadius = 5f;

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

    private const float Gravity      = -12f;
    private const float AlertSustain = 3.5f;   // seconds the guard chases after losing sight
    private float _vertVel;

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
    }

    void Update()
    {
        if (_hasCaught) return;
        ApplyGravity();
        SensePlayer();

        switch (State)
        {
            case GuardState.Patrol:  UpdatePatrol();  break;
            case GuardState.Chase:   UpdateChase();   break;
            case GuardState.Alerted: UpdateAlerted(); break;
        }
    }

    // ── Gravity ───────────────────────────────────────────────────────────────

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _vertVel < 0f) _vertVel = -2f;
        _vertVel += Gravity * Time.deltaTime;
    }

    // ── Sensing ───────────────────────────────────────────────────────────────

    private void SensePlayer()
    {
        if (_player == null) return;
        if (CanSeePlayer() || CanHearPlayer())
            EnterChase();
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
        _alertTimer = AlertSustain;
    }

    private void UpdateChase()
    {
        if (_player == null) return;
        MoveToward(_player.position, chaseSpeed);

        if (Vector3.Distance(transform.position, _player.position) <= catchDistance)
        {
            _hasCaught = true;
            ExplorationSceneManager.Instance?.OnPlayerCaught();
            return;
        }

        if (!CanSeePlayer() && !CanHearPlayer())
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f)
                State = GuardState.Alerted;
        }
        else
        {
            _alertTimer = AlertSustain;
        }
    }

    // ── Alerted (heading to last known pos before giving up) ──────────────────

    private void UpdateAlerted()
    {
        if (_player == null) { State = GuardState.Patrol; return; }
        MoveToward(_player.position, moveSpeed * 1.2f);
        _alertTimer -= Time.deltaTime;
        if (_alertTimer <= 0f)
            State = GuardState.Patrol;
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

    // ── Reset (called by ExplorationSceneManager on retry) ───────────────────

    public void ResetGuard()
    {
        _hasCaught     = false;
        _waiting       = false;
        _alertTimer    = 0f;
        _waypointIndex = 0;
        State          = GuardState.Patrol;
        StopAllCoroutines();
    }
}
