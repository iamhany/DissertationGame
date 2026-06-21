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

    [Header("Obstacle Jumping")]
    [Tooltip("Vertical impulse guards use when stepping over low obstacles.")]
    public float obstacleJumpForce = 4.8f;
    [Tooltip("How far ahead guards check for a low obstacle.")]
    public float obstacleProbeDistance = 0.9f;
    [Tooltip("Height of the low forward check, measured from the guard feet.")]
    public float obstacleProbeHeight = 0.45f;
    [Tooltip("Height that must be clear above the obstacle before the guard jumps.")]
    public float obstacleClearanceHeight = 1.25f;
    [Tooltip("Minimum seconds between automatic obstacle jumps.")]
    public float obstacleJumpCooldown = 0.75f;

    [Header("Unstuck")]
    [Tooltip("How long a guard can fail to move before choosing an escape direction.")]
    public float stuckTimeBeforeRepath = 0.65f;
    [Tooltip("Seconds spent sidestepping or backing away before trying the target again.")]
    public float unstuckDuration = 0.9f;
    [Tooltip("Minimum horizontal movement per second before a guard counts as stuck.")]
    public float stuckMinMoveSpeed = 0.08f;

    [Header("Animation")]
    [Tooltip("Animator on the guard mesh. Needs float 'Speed' and bool 'Alerted'.")]
    public Animator guardAnimator;
    [Tooltip("Small torso correction used by the procedural fallback posture.")]
    public float proceduralTorsoCorrection = 0f;
    [Tooltip("Humanoid upper-leg muscle delta used only while moving.")]
    public float proceduralLegStepMuscle = 0.58f;
    [Tooltip("Humanoid lower-leg bend delta used only while moving.")]
    public float proceduralKneeStepMuscle = 0.42f;

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
    private float _nextObstacleJumpTime;
    private float _stuckTimer;
    private float _unstuckUntil;
    private Vector3 _unstuckDirection;
    // Grace period (seconds) before guards start sensing, to prevent
    // false catches during scene-load frame spikes.
    private float _senseDelay = 2f;

    private bool _canAnimate;
    private bool _useProceduralAnimation;
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int AlertedParam = Animator.StringToHash("Alerted");

    private HumanPoseHandler _poseHandler;
    private HumanPose        _humanPose;
    private float[]          _neutralMuscles;
    private int _leftArmDownUp = -1;
    private int _rightArmDownUp = -1;
    private int _leftArmFrontBack = -1;
    private int _rightArmFrontBack = -1;
    private int _leftForearmStretch = -1;
    private int _rightForearmStretch = -1;
    private int _leftLegFrontBack = -1;
    private int _rightLegFrontBack = -1;
    private int _leftKneeStretch = -1;
    private int _rightKneeStretch = -1;
    private int _spineFrontBack = -1;
    private int _chestFrontBack = -1;
    private int _upperChestFrontBack = -1;
    private int _neckLeftRight = -1;

    private float _animationTime;

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
        _canAnimate = CanDriveAnimator();
        _useProceduralAnimation = CacheProceduralBones();
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

        if (_canAnimate)
        {
            guardAnimator.SetFloat(SpeedParam,   speed);
            guardAnimator.SetBool (AlertedParam, State == GuardState.Alerted);
        }

        if (_useProceduralAnimation)
        {
            UpdateProceduralAnimation(speed);
        }
    }

    private bool CanDriveAnimator()
    {
        if (guardAnimator == null || guardAnimator.runtimeAnimatorController == null)
            return false;

        bool hasSpeed = false;
        bool hasAlerted = false;

        foreach (AnimatorControllerParameter parameter in guardAnimator.parameters)
        {
            if (parameter.nameHash == SpeedParam && parameter.type == AnimatorControllerParameterType.Float)
                hasSpeed = true;
            if (parameter.nameHash == AlertedParam && parameter.type == AnimatorControllerParameterType.Bool)
                hasAlerted = true;
        }

        return hasSpeed && hasAlerted;
    }

    private bool CacheProceduralBones()
    {
        if (guardAnimator == null || guardAnimator.avatar == null || !guardAnimator.avatar.isHuman)
            return false;

        _poseHandler = new HumanPoseHandler(guardAnimator.avatar, guardAnimator.transform);
        _poseHandler.GetHumanPose(ref _humanPose);

        _leftArmDownUp = FindMuscle("Left Arm Down-Up");
        _rightArmDownUp = FindMuscle("Right Arm Down-Up");
        _leftArmFrontBack = FindMuscle("Left Arm Front-Back");
        _rightArmFrontBack = FindMuscle("Right Arm Front-Back");
        _leftForearmStretch = FindMuscle("Left Forearm Stretch");
        _rightForearmStretch = FindMuscle("Right Forearm Stretch");
        _leftLegFrontBack = FindMuscle("Left Upper Leg Front-Back");
        _rightLegFrontBack = FindMuscle("Right Upper Leg Front-Back");
        _leftKneeStretch = FindMuscle("Left Lower Leg Stretch");
        _rightKneeStretch = FindMuscle("Right Lower Leg Stretch");
        _spineFrontBack = FindMuscle("Spine Front-Back");
        _chestFrontBack = FindMuscle("Chest Front-Back");
        _upperChestFrontBack = FindMuscle("Upper Chest Front-Back");
        _neckLeftRight = FindMuscle("Neck Left-Right");
        _neutralMuscles = (float[])_humanPose.muscles.Clone();

        bool hasCoreMuscles =
            _leftArmDownUp >= 0 && _rightArmDownUp >= 0;

        if (hasCoreMuscles)
            ApplyProceduralPose(0f, 0f, 0f, 0f);

        return hasCoreMuscles;
    }

    private int FindMuscle(string muscleName)
    {
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            if (HumanTrait.MuscleName[i] == muscleName)
                return i;
        }

        return -1;
    }

    private void UpdateProceduralAnimation(float speed)
    {
        float moving = speed > 0.05f && !_waiting ? 1f : 0f;
        float strideSpeed = State == GuardState.Chase ? 8.5f : 5.5f;
        float strideAmount = State == GuardState.Chase ? 1.15f : 0.82f;
        _animationTime += Time.deltaTime * strideSpeed * Mathf.Max(0.25f, moving);

        float stride = Mathf.Sin(_animationTime) * moving * strideAmount;
        float leftStepBend = Mathf.Clamp01((Mathf.Cos(_animationTime) - 0.35f) / 0.65f) * moving;
        float rightStepBend = Mathf.Clamp01((-Mathf.Cos(_animationTime) - 0.35f) / 0.65f) * moving;
        float alert = State == GuardState.Alerted ? 1f : 0f;

        ApplyProceduralPose(stride, leftStepBend, rightStepBend, alert);
    }

    private void ApplyProceduralPose(float stride, float leftStepBend, float rightStepBend, float alert)
    {
        if (_poseHandler == null) return;

        if (_neutralMuscles == null) return;

        float[] muscles = (float[])_neutralMuscles.Clone();

        SetMuscle(muscles, _leftArmDownUp, -0.65f);
        SetMuscle(muscles, _rightArmDownUp, -0.65f);
        SetMuscle(muscles, _leftArmFrontBack, stride * -0.22f);
        SetMuscle(muscles, _rightArmFrontBack, stride * 0.22f);
        SetMuscle(muscles, _leftForearmStretch, 0.45f + alert * 0.12f);
        SetMuscle(muscles, _rightForearmStretch, 0.45f + alert * 0.12f);

        if (Mathf.Abs(stride) > 0.001f || leftStepBend > 0.001f || rightStepBend > 0.001f)
        {
            SetMuscle(muscles, _leftLegFrontBack, stride * proceduralLegStepMuscle);
            SetMuscle(muscles, _rightLegFrontBack, stride * -proceduralLegStepMuscle);
            SetMuscle(muscles, _leftKneeStretch, 0.85f - leftStepBend * proceduralKneeStepMuscle);
            SetMuscle(muscles, _rightKneeStretch, 0.85f - rightStepBend * proceduralKneeStepMuscle);
        }

        SetMuscle(muscles, _spineFrontBack, proceduralTorsoCorrection);
        SetMuscle(muscles, _chestFrontBack, proceduralTorsoCorrection * 0.5f);
        SetMuscle(muscles, _upperChestFrontBack, 0f);
        SetMuscle(muscles, _neckLeftRight, Mathf.Sin(_animationTime * 0.5f) * alert * 0.35f);

        _humanPose.muscles = muscles;
        _poseHandler.SetHumanPose(ref _humanPose);
    }

    private void SetMuscle(float[] muscles, int index, float value)
    {
        if (index < 0 || index >= muscles.Length) return;
        muscles[index] = Mathf.Clamp(value, -1f, 1f);
    }

    private void AddMuscle(float[] muscles, int index, float delta)
    {
        if (index < 0 || index >= muscles.Length) return;
        muscles[index] = Mathf.Clamp(muscles[index] + delta, -1f, 1f);
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
        Vector3 desiredDir = target - transform.position;
        desiredDir.y = 0f;

        Vector3 moveDir = desiredDir;
        if (moveDir.magnitude > 0.05f)
        {
            moveDir.Normalize();

            if (Time.time < _unstuckUntil)
            {
                moveDir = _unstuckDirection;
            }
            else
            {
                if (!TryJumpObstacle(moveDir) && IsBlockedAhead(moveDir))
                    BeginUnstuck(moveDir);

                if (Time.time < _unstuckUntil)
                    moveDir = _unstuckDirection;
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(moveDir), 8f * Time.deltaTime);
        }

        Vector3 before = transform.position;
        _cc.Move((moveDir.normalized * speed + Vector3.up * _vertVel) * Time.deltaTime);
        UpdateStuckDetection(before, desiredDir, speed);
    }

    private bool TryJumpObstacle(Vector3 direction)
    {
        if (!_cc.isGrounded || Time.time < _nextObstacleJumpTime) return false;
        if (direction.sqrMagnitude < 0.001f) return false;

        int mask = obstacleMask.value != 0 ? obstacleMask.value : ~0;
        float probeRadius = Mathf.Max(0.08f, _cc.radius * 0.55f);
        Vector3 lowOrigin = transform.position + Vector3.up * obstacleProbeHeight;
        Vector3 highOrigin = transform.position + Vector3.up * obstacleClearanceHeight;

        if (!Physics.SphereCast(lowOrigin, probeRadius, direction, out var lowHit,
                obstacleProbeDistance, mask, QueryTriggerInteraction.Ignore))
            return false;

        if (IsOwnOrPlayerTransform(lowHit.transform)) return false;

        bool blockedAtChest = Physics.SphereCast(highOrigin, probeRadius, direction,
            out _, obstacleProbeDistance, mask, QueryTriggerInteraction.Ignore);
        if (blockedAtChest) return false;

        _vertVel = obstacleJumpForce;
        _nextObstacleJumpTime = Time.time + obstacleJumpCooldown;
        return true;
    }

    private void UpdateStuckDetection(Vector3 before, Vector3 desiredDirection, float speed)
    {
        if (Time.time < _unstuckUntil || desiredDirection.sqrMagnitude < 0.01f || speed <= 0.05f)
        {
            _stuckTimer = 0f;
            return;
        }

        Vector3 moved = transform.position - before;
        moved.y = 0f;
        float minMove = stuckMinMoveSpeed * Time.deltaTime;
        if (moved.magnitude < minMove)
            _stuckTimer += Time.deltaTime;
        else
            _stuckTimer = 0f;

        if (_stuckTimer >= stuckTimeBeforeRepath)
        {
            Vector3 dir = desiredDirection;
            dir.y = 0f;
            BeginUnstuck(dir.normalized);
        }
    }

    private bool IsBlockedAhead(Vector3 direction)
    {
        int mask = obstacleMask.value != 0 ? obstacleMask.value : ~0;
        float probeRadius = Mathf.Max(0.08f, _cc.radius * 0.55f);
        Vector3 origin = transform.position + Vector3.up * obstacleClearanceHeight;

        if (!Physics.SphereCast(origin, probeRadius, direction, out var hit,
                obstacleProbeDistance, mask, QueryTriggerInteraction.Ignore))
            return false;

        return !IsOwnOrPlayerTransform(hit.transform);
    }

    private void BeginUnstuck(Vector3 blockedDirection)
    {
        if (blockedDirection.sqrMagnitude < 0.001f) return;

        Vector3 right = Vector3.Cross(Vector3.up, blockedDirection).normalized;
        Vector3 left = -right;
        Vector3 back = -blockedDirection.normalized;

        if (!IsBlockedAhead(right))
            _unstuckDirection = right;
        else if (!IsBlockedAhead(left))
            _unstuckDirection = left;
        else
            _unstuckDirection = back;

        _stuckTimer = 0f;
        _unstuckUntil = Time.time + unstuckDuration;
    }

    private bool IsOwnOrPlayerTransform(Transform hitTransform)
    {
        if (hitTransform == null) return false;
        if (hitTransform == transform || hitTransform.IsChildOf(transform)) return true;
        return _player != null && (hitTransform == _player || hitTransform.IsChildOf(_player));
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public void ResetGuard()
    {
        _hasCaught     = false;
        _waiting       = false;
        _alertTimer    = 0f;
        _waypointIndex = 0;
        _hasLastKnown  = false;
        _stuckTimer    = 0f;
        _unstuckUntil  = 0f;
        _unstuckDirection = Vector3.zero;
        State          = GuardState.Patrol;
        StopAllCoroutines();
        if (_canAnimate)
        {
            guardAnimator.SetFloat(SpeedParam,   0f);
            guardAnimator.SetBool (AlertedParam, false);
        }
        if (_useProceduralAnimation)
        {
            _animationTime = 0f;
            ApplyProceduralPose(0f, 0f, 0f, 0f);
        }
    }
}
