using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages the Garden of Gethsemane first-person exploration / escape scene.
///
/// Flow:
///   • Loads Synty Demo additively for the environment.
///   • Finds all JumpPassZone objects in the scene.
///   • Player must jump over every JumpPassZone while evading GuardControllers.
///   • All zones cleared → success → narrative resumes.
///   • Caught by a guard  → retry panel shown.
///   • No zones present   → free-roam; press [E] to finish.
/// </summary>
public class ExplorationSceneManager : MonoBehaviour
{
    public static ExplorationSceneManager Instance { get; private set; }

    private const string SyntyDemoSceneName = "Demo";
    private const int MaxActiveGuards = 25;
    private const float GuardPatrolSpeed = 0.95f;
    private const float GuardChaseSpeed = 3.25f;
    private const float GuardAlertedSpeed = 1.9f;
    private const float GuardCatchDistance = 1.15f;
    private const float GuardMeterFarDistance = 24f;
    private const int MaxRearSoundIndicators = 3;

    [Header("UI")]
    public TMP_Text   instructionText;
    public TMP_Text   progressText;
    public GameObject continuePanel;
    public TMP_Text   continuePanelMessage;
    public Button     continueButton;
    public GameObject retryPanel;
    public Button     retryButton;
    [Tooltip("Testing only — skips the level immediately.")]
    public Button     skipButton;

    // ── Private state ─────────────────────────────────────────────────────────

    private FirstPersonController _player;
    private Vector3               _playerStartPos;
    private Quaternion            _playerStartRot;
    private Quaternion            _playerCameraStartLocalRot;

    private JumpPassZone[]    _zones;
    private int               _clearedCount;

    private GuardController[] _guards;
    private Vector3[]         _guardStartPos;
    private Quaternion[]      _guardStartRot;
    private bool[]            _guardStartsActive;

    private EscapeGate[]      _gates;
    private GameObject        _gateArrowGroup;
    private RectTransform     _gateArrow;
    private Image             _gateArrowImage;
    private TMP_Text          _gateArrowInstruction;
    private GameObject        _guardMeterGroup;
    private Image             _guardMeterFill;
    private TMP_Text          _guardMeterLabel;
    private GameObject        _nitroMeterGroup;
    private Image             _nitroMeterFill;
    private TMP_Text          _nitroMeterLabel;
    private GameObject        _rearGuardSoundGroup;
    private GameObject[]      _rearGuardSoundIndicators;
    private RectTransform[]   _rearGuardSoundArrows;
    private RectTransform[]   _rearGuardSoundIndicatorRects;
    private TMP_Text[]        _rearGuardSoundLabels;

    private bool _sceneDone;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GuardAlertNetwork.Clear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Additive) return;

        // sceneLoaded fires before Start() on the newly loaded scene's objects.
        // Disabling here prevents FMOD from switching audio output, duplicate
        // cameras rendering, or duplicate EventSystems processing input.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                cam.enabled = false;
            foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
                al.enabled = false;
            foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
            {
                src.Stop();
                src.enabled = false;
            }
        }
        DisableInvalidBoxColliders(scene);

        // Kill duplicate EventSystems immediately (SetActive false so they don't
        // process input even for the remainder of this frame).
        var allES = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        for (int i = 1; i < allES.Length; i++)
        {
            allES[i].gameObject.SetActive(false);
            Destroy(allES[i].gameObject);
        }
    }

    IEnumerator Start()
    {
        var op = SceneManager.LoadSceneAsync(SyntyDemoSceneName, LoadSceneMode.Additive);
        yield return op;

        Scene demoScene = SceneManager.GetSceneByName(SyntyDemoSceneName);

        if (demoScene.IsValid() && demoScene.isLoaded)
        {
             SceneManager.SetActiveScene(demoScene);
            ApplyDemoLighting(demoScene);
        }

        // Camera / AudioListener / AudioSource suppression already handled in OnSceneLoaded.

        _player = FindFirstObjectByType<FirstPersonController>();
        if (_player != null)
        {
            _playerStartPos = _player.transform.position;
            _playerStartRot = _player.transform.rotation;
            _playerCameraStartLocalRot = _player.cameraTransform != null
                ? _player.cameraTransform.localRotation
                : Quaternion.identity;
        }

        _zones        = FindObjectsByType<JumpPassZone>(FindObjectsSortMode.None);
        _clearedCount = 0;

        _gates = FindObjectsByType<EscapeGate>(FindObjectsSortMode.None);
        BuildGateDirectionArrow();
        BuildGuardDistanceMeter();
        BuildNitroMeter();
        BuildRearGuardSoundIndicator();

        _guards        = FindObjectsByType<GuardController>(FindObjectsSortMode.None);
        ConfigureGuardLayout();
        _guardStartPos = new Vector3[_guards.Length];
        _guardStartRot = new Quaternion[_guards.Length];
        _guardStartsActive = new bool[_guards.Length];
        for (int i = 0; i < _guards.Length; i++)
        {
            _guardStartPos[i] = _guards[i].transform.position;
            _guardStartRot[i] = _guards[i].transform.rotation;
            _guardStartsActive[i] = _guards[i].gameObject.activeSelf;
        }

        if (continuePanel != null) continuePanel.SetActive(false);
        if (retryPanel    != null) retryPanel.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (retryButton    != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (progressText    != null) progressText.gameObject.SetActive(false);
        if (skipButton     != null) skipButton.gameObject.SetActive(false);

        RefreshUI();
    }

    void Update()
    {
        UpdateGateDirectionArrow();
        UpdateGuardDistanceMeter();
        UpdateNitroMeter();
        UpdateRearGuardSoundIndicator();

        if (_sceneDone) return;

        if (Keyboard.current?.qKey.wasPressedThisFrame == true)
            TriggerSuccess("Level skipped.\nReturn to the story?");
    }

    // ── Public callbacks ──────────────────────────────────────────────────────

    /// <summary>Called by EscapeGate when the player reaches the exit.</summary>
    public void OnEscapeGateReached()
    {
        TriggerSuccess("You escaped the Garden!\nReturn to the story?");
    }

    /// <summary>Called by JumpPassZone when the player clears it.</summary>
    public void OnJumpPassZoneCleared(JumpPassZone zone)
    {
        _clearedCount++;
        RefreshUI();
        if (_zones != null && _clearedCount >= _zones.Length)
            TriggerSuccess("You cleared the way!\nReturn to the story?");
    }

    /// <summary>Called by GuardController when the player is caught.</summary>
    public void OnPlayerCaught()
    {
        if (_sceneDone) return;
        _sceneDone = true;
        _player?.LockCursor(false);
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (progressText    != null) progressText.gameObject.SetActive(false);
        if (_gateArrowGroup != null) _gateArrowGroup.SetActive(false);
        if (_guardMeterGroup != null) _guardMeterGroup.SetActive(false);
        if (_nitroMeterGroup != null) _nitroMeterGroup.SetActive(false);
        if (_rearGuardSoundGroup != null) _rearGuardSoundGroup.SetActive(false);
        foreach (var g in _guards) g.enabled = false;
        RespawnPlayerAtStart(true);
        if (retryPanel != null) retryPanel.SetActive(true);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void TriggerSuccess(string message)
    {
        _sceneDone = true;
        _player?.LockCursor(false);
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (progressText    != null) progressText.gameObject.SetActive(false);
        if (_gateArrowGroup != null) _gateArrowGroup.SetActive(false);
        if (_guardMeterGroup != null) _guardMeterGroup.SetActive(false);
        if (_nitroMeterGroup != null) _nitroMeterGroup.SetActive(false);
        if (_rearGuardSoundGroup != null) _rearGuardSoundGroup.SetActive(false);
        if (continuePanelMessage != null) continuePanelMessage.text = message;
        if (continuePanel != null) continuePanel.SetActive(true);
    }

    private void RefreshUI()
    {
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (progressText    != null) progressText.gameObject.SetActive(false);
        if (skipButton      != null) skipButton.gameObject.SetActive(false);
    }

    private void ApplyDemoLighting(Scene demoScene)
    {
        foreach (GameObject root in demoScene.GetRootGameObjects())
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
              if (light.type == LightType.Directional)
              {
                   light.enabled = true;
                  RenderSettings.sun = light;
                  DynamicGI.UpdateEnvironment();
                  return;
              }
            }
        }
    }

    private void DisableInvalidBoxColliders(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.size.x < 0f || box.size.y < 0f || box.size.z < 0f || HasNegativeScaleInHierarchy(box.transform))
                    box.enabled = false;
            }
        }
    }

    private bool HasNegativeScaleInHierarchy(Transform transform)
    {
        Vector3 scaleSign = Vector3.one;
        Transform current = transform;

        while (current != null)
        {
            scaleSign.x *= Mathf.Sign(current.localScale.x);
            scaleSign.y *= Mathf.Sign(current.localScale.y);
            scaleSign.z *= Mathf.Sign(current.localScale.z);
            current = current.parent;
        }

        return scaleSign.x < 0f || scaleSign.y < 0f || scaleSign.z < 0f;
    }

    private void ConfigureGuardLayout()
    {
        if (_guards == null || _guards.Length == 0) return;
        EnsureGuardCount();

        Vector3[] patrolCenters =
        {
            new Vector3(16f, 0f, 4f),
            new Vector3(36f, -1f, 8f),
            new Vector3(58f, -3f, 13f),
            new Vector3(82f, -5f, 18f),
            new Vector3(18f, 0f, -22f),
            new Vector3(28f, 0f, 28f),
            new Vector3(42f, -1f, -38f),
            new Vector3(52f, -2f, 36f),
            new Vector3(66f, -3f, -28f),
            new Vector3(76f, -4f, 34f),
            new Vector3(90f, -5f, -42f),
            new Vector3(102f, -6f, 42f),
            new Vector3(116f, -8f, -30f),
            new Vector3(104f, -7f, -14f),
            new Vector3(12f, 0f, 42f),
            new Vector3(34f, 0f, -4f),
            new Vector3(48f, -1f, 18f),
            new Vector3(62f, -2f, -6f),
            new Vector3(78f, -4f, 2f),
            new Vector3(92f, -5f, 30f),
            new Vector3(110f, -7f, -4f),
            new Vector3(72f, -3f, 48f),
            new Vector3(98f, -6f, -52f),
            new Vector3(118f, -8f, -48f),
            new Vector3(6f, 0f, -10f),
            new Vector3(24f, 0f, 52f),
        };

        for (int i = 0; i < _guards.Length; i++)
        {
            bool active = i < MaxActiveGuards && i < patrolCenters.Length;
            _guards[i].gameObject.SetActive(active);
            if (!active) continue;

            Vector3 center = FindGroundPosition(patrolCenters[i]);
            _guards[i].transform.position = center;
            _guards[i].moveSpeed = GuardPatrolSpeed;
            _guards[i].chaseSpeed = GuardChaseSpeed;
            _guards[i].alertedSpeed = GuardAlertedSpeed;
            _guards[i].catchDistance = GuardCatchDistance;
            _guards[i].proceduralTorsoCorrection = 0f;
            RebuildGuardWaypoints(_guards[i], center, i);
        }
    }

    private void EnsureGuardCount()
    {
        if (_guards.Length >= MaxActiveGuards) return;

        GuardController template = _guards[0];
        var guardList = new List<GuardController>(_guards);
        for (int i = _guards.Length; i < MaxActiveGuards; i++)
        {
            var clone = Instantiate(template.gameObject);
            clone.name = $"Guard_{i + 1}";
            var guard = clone.GetComponent<GuardController>();
            if (guard != null)
                guardList.Add(guard);
        }

        _guards = guardList.ToArray();
    }

    private void RebuildGuardWaypoints(GuardController guard, Vector3 center, int index)
    {
        guard.waypoints.Clear();

        float radius = 7f + (index % 2) * 2f;
        float angleOffset = index * 47f * Mathf.Deg2Rad;
        for (int i = 0; i < 3; i++)
        {
            float angle = angleOffset + i * Mathf.PI * 2f / 3f;
            Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            position = FindGroundPosition(position);

            var waypointGO = new GameObject($"{guard.name}_RuntimeWaypoint_{i + 1}");
            waypointGO.transform.position = position;
            guard.waypoints.Add(waypointGO.transform);
        }
    }

    private Vector3 FindGroundPosition(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 60f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return position;
    }

    private void OnContinueClicked()
    {
        ProphecyManager.Instance?.ApplyIntegrityDelta(5);
        GameManager.Instance.ResumeNarrative();
    }

    private void OnRetryClicked()
    {
        _sceneDone    = false;
        _clearedCount = 0;

        if (_zones != null)
            foreach (var z in _zones) z.ResetZone();

        for (int i = 0; i < _guards.Length; i++)
        {
            bool active = _guardStartsActive == null || i >= _guardStartsActive.Length || _guardStartsActive[i];
            _guards[i].gameObject.SetActive(active);
            if (!active) continue;

            _guards[i].transform.SetPositionAndRotation(_guardStartPos[i], _guardStartRot[i]);
            _guards[i].ResetGuard();
            _guards[i].enabled = true;
        }

        RespawnPlayerAtStart(true);
        _player?.LockCursor(true);

        if (retryPanel      != null) retryPanel.SetActive(false);
        if (instructionText != null) instructionText.gameObject.SetActive(true);
        if (progressText    != null) progressText.gameObject.SetActive(true);
        if (_gateArrowGroup != null) _gateArrowGroup.SetActive(_gates != null && _gates.Length > 0);
        RefreshUI();
    }

    private void RespawnPlayerAtStart(bool resetVelocity)
    {
        if (_player == null) return;

        var controller = _player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        _player.transform.position = _playerStartPos;
        _player.ResetLook(_playerStartRot, _playerCameraStartLocalRot);
        if (resetVelocity)
            _player.ResetVerticalVelocity();
        _player.ResetNitro();

        if (controller != null) controller.enabled = true;
    }

    private Canvas GetHudCanvas()
    {
        Canvas canvas = instructionText != null
            ? instructionText.GetComponentInParent<Canvas>(true)
            : null;
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        return canvas;
    }

    private void BuildGuardDistanceMeter()
    {
        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        _guardMeterGroup = new GameObject("GuardDistanceMeter");
        _guardMeterGroup.transform.SetParent(canvas.transform, false);
        var groupRT = _guardMeterGroup.AddComponent<RectTransform>();
        groupRT.anchorMin = new Vector2(0.5f, 0.97f);
        groupRT.anchorMax = new Vector2(0.5f, 0.97f);
        groupRT.pivot = new Vector2(0.5f, 1f);
        groupRT.anchoredPosition = Vector2.zero;
        groupRT.sizeDelta = new Vector2(320f, 44f);

        var panelGO = new GameObject("GuardDistancePanel");
        panelGO.transform.SetParent(_guardMeterGroup.transform, false);
        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        var fillBackGO = new GameObject("MeterTrack");
        fillBackGO.transform.SetParent(_guardMeterGroup.transform, false);
        var fillBackImage = fillBackGO.AddComponent<Image>();
        fillBackImage.color = new Color(0.12f, 0.18f, 0.2f, 0.9f);
        fillBackImage.raycastTarget = false;
        var fillBackRT = fillBackGO.GetComponent<RectTransform>();
        fillBackRT.anchorMin = new Vector2(0.05f, 0.22f);
        fillBackRT.anchorMax = new Vector2(0.95f, 0.52f);
        fillBackRT.offsetMin = Vector2.zero;
        fillBackRT.offsetMax = Vector2.zero;

        var fillGO = new GameObject("MeterFill");
        fillGO.transform.SetParent(fillBackGO.transform, false);
        _guardMeterFill = fillGO.AddComponent<Image>();
        _guardMeterFill.color = new Color(0.2f, 0.75f, 1f, 0.95f);
        _guardMeterFill.type = Image.Type.Filled;
        _guardMeterFill.fillMethod = Image.FillMethod.Horizontal;
        _guardMeterFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _guardMeterFill.fillAmount = 0f;
        _guardMeterFill.raycastTarget = false;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var labelGO = new GameObject("MeterLabel");
        labelGO.transform.SetParent(_guardMeterGroup.transform, false);
        _guardMeterLabel = labelGO.AddComponent<TextMeshProUGUI>();
        _guardMeterLabel.text = "GUARDS NEARBY";
        _guardMeterLabel.fontSize = 13f;
        _guardMeterLabel.color = new Color(0.92f, 0.97f, 1f, 0.96f);
        _guardMeterLabel.alignment = TextAlignmentOptions.Center;
        _guardMeterLabel.raycastTarget = false;
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.54f);
        labelRT.anchorMax = new Vector2(1f, 0.98f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        _guardMeterGroup.SetActive(false);
    }

    private void BuildNitroMeter()
    {
        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        _nitroMeterGroup = new GameObject("NitroMeter");
        _nitroMeterGroup.transform.SetParent(canvas.transform, false);
        var groupRT = _nitroMeterGroup.AddComponent<RectTransform>();
        groupRT.anchorMin = new Vector2(0.02f, 0.04f);
        groupRT.anchorMax = new Vector2(0.02f, 0.04f);
        groupRT.pivot = new Vector2(0f, 0f);
        groupRT.anchoredPosition = Vector2.zero;
        groupRT.sizeDelta = new Vector2(210f, 58f);

        var panelGO = new GameObject("NitroPanel");
        panelGO.transform.SetParent(_nitroMeterGroup.transform, false);
        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0.08f, 0.07f, 0.76f);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        var trackGO = new GameObject("NitroTrack");
        trackGO.transform.SetParent(_nitroMeterGroup.transform, false);
        var trackImage = trackGO.AddComponent<Image>();
        trackImage.color = new Color(0.04f, 0.14f, 0.13f, 0.95f);
        trackImage.raycastTarget = false;
        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0.08f, 0.18f);
        trackRT.anchorMax = new Vector2(0.92f, 0.46f);
        trackRT.offsetMin = Vector2.zero;
        trackRT.offsetMax = Vector2.zero;

        var fillGO = new GameObject("NitroFill");
        fillGO.transform.SetParent(trackGO.transform, false);
        _nitroMeterFill = fillGO.AddComponent<Image>();
        _nitroMeterFill.color = new Color(0.2f, 1f, 0.72f, 0.95f);
        _nitroMeterFill.type = Image.Type.Filled;
        _nitroMeterFill.fillMethod = Image.FillMethod.Horizontal;
        _nitroMeterFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _nitroMeterFill.fillAmount = 1f;
        _nitroMeterFill.raycastTarget = false;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var labelGO = new GameObject("NitroLabel");
        labelGO.transform.SetParent(_nitroMeterGroup.transform, false);
        _nitroMeterLabel = labelGO.AddComponent<TextMeshProUGUI>();
        _nitroMeterLabel.text = "CTRL NITRO";
        _nitroMeterLabel.fontSize = 14f;
        _nitroMeterLabel.fontStyle = FontStyles.Bold;
        _nitroMeterLabel.color = new Color(0.9f, 1f, 0.95f, 0.96f);
        _nitroMeterLabel.alignment = TextAlignmentOptions.Center;
        _nitroMeterLabel.raycastTarget = false;
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.5f);
        labelRT.anchorMax = new Vector2(1f, 0.96f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
    }

    private void UpdateNitroMeter()
    {
        if (_nitroMeterGroup == null || _nitroMeterFill == null || _player == null || _sceneDone)
        {
            if (_nitroMeterGroup != null) _nitroMeterGroup.SetActive(false);
            return;
        }

        _nitroMeterGroup.SetActive(true);
        float charge = Mathf.Clamp01(_player.NitroCharge01);
        _nitroMeterFill.fillAmount = charge;
        _nitroMeterFill.color = Color.Lerp(
            new Color(0.25f, 0.35f, 0.34f, 0.95f),
            new Color(0.2f, 1f, 0.72f, 0.95f),
            charge);

        if (_nitroMeterLabel != null)
            _nitroMeterLabel.text = _player.IsNitroActive ? "CTRL NITRO  ACTIVE" : "CTRL NITRO";
    }

    private void BuildRearGuardSoundIndicator()
    {
        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        _rearGuardSoundGroup = new GameObject("RearGuardSoundIndicator");
        _rearGuardSoundGroup.transform.SetParent(canvas.transform, false);
        var groupRT = _rearGuardSoundGroup.AddComponent<RectTransform>();
        groupRT.anchorMin = new Vector2(0.5f, 0.29f);
        groupRT.anchorMax = new Vector2(0.5f, 0.29f);
        groupRT.pivot = new Vector2(0.5f, 0.5f);
        groupRT.anchoredPosition = Vector2.zero;
        groupRT.sizeDelta = new Vector2(420f, 112f);

        _rearGuardSoundIndicators = new GameObject[MaxRearSoundIndicators];
        _rearGuardSoundArrows = new RectTransform[MaxRearSoundIndicators];
        _rearGuardSoundIndicatorRects = new RectTransform[MaxRearSoundIndicators];
        _rearGuardSoundLabels = new TMP_Text[MaxRearSoundIndicators];

        for (int i = 0; i < MaxRearSoundIndicators; i++)
        {
            var indicatorGO = new GameObject($"SoundIndicator_{i + 1}");
            indicatorGO.transform.SetParent(_rearGuardSoundGroup.transform, false);
            _rearGuardSoundIndicators[i] = indicatorGO;
            var indicatorRT = indicatorGO.AddComponent<RectTransform>();
            indicatorRT.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRT.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRT.pivot = new Vector2(0.5f, 0.5f);
            indicatorRT.sizeDelta = new Vector2(150f, 104f);
            _rearGuardSoundIndicatorRects[i] = indicatorRT;

            var ringGO = new GameObject("SoundRing");
            ringGO.transform.SetParent(indicatorGO.transform, false);
            var ringImage = ringGO.AddComponent<Image>();
            ringImage.sprite = CreateRingSprite();
            ringImage.color = new Color(1f, 0.55f, 0.14f, 0.58f);
            ringImage.raycastTarget = false;
            var ringRT = ringGO.GetComponent<RectTransform>();
            ringRT.anchorMin = new Vector2(0.5f, 0.66f);
            ringRT.anchorMax = new Vector2(0.5f, 0.66f);
            ringRT.pivot = new Vector2(0.5f, 0.5f);
            ringRT.sizeDelta = new Vector2(68f, 68f);

            var arrowGO = new GameObject("SoundArrow");
            arrowGO.transform.SetParent(indicatorGO.transform, false);
            var arrowImage = arrowGO.AddComponent<Image>();
            arrowImage.sprite = CreateArrowSprite();
            arrowImage.color = new Color(1f, 0.42f, 0.1f, 0.98f);
            arrowImage.preserveAspect = true;
            arrowImage.raycastTarget = false;
            var arrowRT = arrowGO.GetComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(0.5f, 0.66f);
            arrowRT.anchorMax = new Vector2(0.5f, 0.66f);
            arrowRT.pivot = new Vector2(0.5f, 0.5f);
            arrowRT.sizeDelta = new Vector2(54f, 54f);
            _rearGuardSoundArrows[i] = arrowRT;

            var labelGO = new GameObject("SoundLabel");
            labelGO.transform.SetParent(indicatorGO.transform, false);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "FOOTSTEPS BEHIND";
            label.fontSize = 13f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(1f, 0.82f, 0.56f, 0.96f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 0.36f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            _rearGuardSoundLabels[i] = label;

            indicatorGO.SetActive(false);
        }

        _rearGuardSoundGroup.SetActive(false);
    }

    private void UpdateRearGuardSoundIndicator()
    {
        if (_rearGuardSoundGroup == null || _rearGuardSoundArrows == null || _player == null || _sceneDone)
        {
            if (_rearGuardSoundGroup != null) _rearGuardSoundGroup.SetActive(false);
            return;
        }

        float[] angles = new float[MaxRearSoundIndicators];
        float[] distances = new float[MaxRearSoundIndicators];
        int count = 0;
        for (int i = 0; i < distances.Length; i++)
            distances[i] = float.PositiveInfinity;

        Vector3 playerPosition = _player.transform.position;
        Vector3 forward = _player.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        if (_guards != null && forward.sqrMagnitude > 0.001f)
        {
            foreach (GuardController guard in _guards)
            {
                if (guard == null || !guard.gameObject.activeInHierarchy || guard.State != GuardController.GuardState.Chase)
                    continue;

                Vector3 toGuard = guard.transform.position - playerPosition;
                toGuard.y = 0f;
                if (toGuard.sqrMagnitude < 0.001f) continue;
                Vector3 directionToGuard = toGuard.normalized;

                if (Vector3.Dot(forward, directionToGuard) >= -0.15f)
                    continue;

                float signedAngle = Vector3.SignedAngle(forward, directionToGuard, Vector3.up);
                float sqrDistance = toGuard.sqrMagnitude;
                AddRearSoundCandidate(angles, distances, ref count, signedAngle, sqrDistance);
            }
        }

        bool show = count > 0;
        _rearGuardSoundGroup.SetActive(show);
        for (int i = 0; i < MaxRearSoundIndicators; i++)
        {
            bool active = show && i < count;
            _rearGuardSoundIndicators[i].SetActive(active);
            if (!active) continue;

            float signedAngle = angles[i];
            _rearGuardSoundArrows[i].localRotation = Quaternion.Euler(0f, 0f, -signedAngle);
            float sideOffset = Mathf.Clamp(signedAngle / 180f, -1f, 1f) * 170f;
            _rearGuardSoundIndicatorRects[i].anchoredPosition = new Vector2(sideOffset, i * -8f);

            float distance = Mathf.Sqrt(distances[i]);
            _rearGuardSoundLabels[i].text = distance < 10f ? "FOOTSTEPS CLOSE" : "FOOTSTEPS BEHIND";
        }
    }

    private void AddRearSoundCandidate(float[] angles, float[] distances, ref int count, float angle, float sqrDistance)
    {
        const float MinAngleSeparation = 35f;

        for (int i = 0; i < count; i++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angles[i], angle)) > MinAngleSeparation)
                continue;

            if (sqrDistance < distances[i])
            {
                angles[i] = angle;
                distances[i] = sqrDistance;
            }
            return;
        }

        if (count < angles.Length)
        {
            angles[count] = angle;
            distances[count] = sqrDistance;
            count++;
            return;
        }

        int farthestIndex = 0;
        for (int i = 1; i < distances.Length; i++)
        {
            if (distances[i] > distances[farthestIndex])
                farthestIndex = i;
        }

        if (sqrDistance < distances[farthestIndex])
        {
            angles[farthestIndex] = angle;
            distances[farthestIndex] = sqrDistance;
        }
    }

    private void UpdateGuardDistanceMeter()
    {
        if (_guardMeterGroup == null || _guardMeterFill == null || _player == null || _sceneDone)
        {
            if (_guardMeterGroup != null) _guardMeterGroup.SetActive(false);
            return;
        }

        GuardController nearestChaser = null;
        float nearestDistance = float.PositiveInfinity;
        if (_guards != null)
        {
            foreach (GuardController guard in _guards)
            {
                if (guard == null || !guard.gameObject.activeInHierarchy || guard.State != GuardController.GuardState.Chase)
                    continue;

                float distance = Vector3.Distance(guard.transform.position, _player.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestChaser = guard;
                }
            }
        }

        bool isChased = nearestChaser != null;
        _guardMeterGroup.SetActive(isChased);
        if (!isChased) return;

        float danger = Mathf.InverseLerp(GuardMeterFarDistance, GuardCatchDistance, nearestDistance);
        _guardMeterFill.fillAmount = Mathf.Clamp01(danger);
        _guardMeterFill.color = Color.Lerp(
            new Color(0.2f, 0.75f, 1f, 0.95f),
            new Color(1f, 0.18f, 0.1f, 0.98f),
            danger);

        if (_guardMeterLabel != null)
            _guardMeterLabel.text = $"GUARD DISTANCE  {nearestDistance:0}m";
    }

    private void BuildGateDirectionArrow()
    {
        if (_gates == null || _gates.Length == 0) return;

        Canvas canvas = GetHudCanvas();
        if (canvas == null) return;

        _gateArrowGroup = new GameObject("GateDirectionGuide");
        _gateArrowGroup.transform.SetParent(canvas.transform, false);
        var groupRT = _gateArrowGroup.AddComponent<RectTransform>();
        groupRT.anchorMin = new Vector2(0.5f, 0.08f);
        groupRT.anchorMax = new Vector2(0.5f, 0.08f);
        groupRT.pivot = new Vector2(0.5f, 0f);
        groupRT.anchoredPosition = Vector2.zero;
        groupRT.sizeDelta = new Vector2(360f, 128f);

        var ringGO = new GameObject("ArrowRing");
        ringGO.transform.SetParent(_gateArrowGroup.transform, false);
        var ringImage = ringGO.AddComponent<Image>();
        ringImage.sprite = CreateRingSprite();
        ringImage.color = new Color(0.02f, 0.08f, 0.14f, 0.58f);
        ringImage.raycastTarget = false;
        var ringRT = ringGO.GetComponent<RectTransform>();
        ringRT.anchorMin = new Vector2(0.5f, 1f);
        ringRT.anchorMax = new Vector2(0.5f, 1f);
        ringRT.pivot = new Vector2(0.5f, 0.5f);
        ringRT.anchoredPosition = new Vector2(0f, -38f);
        ringRT.sizeDelta = new Vector2(82f, 82f);

        var arrowGO = new GameObject("GateDirectionArrow");
        arrowGO.transform.SetParent(_gateArrowGroup.transform, false);

        _gateArrowImage = arrowGO.AddComponent<Image>();
        _gateArrowImage.sprite = CreateArrowSprite();
        _gateArrowImage.color = new Color(0.18f, 0.78f, 1f, 0.98f);
        _gateArrowImage.preserveAspect = true;
        _gateArrowImage.raycastTarget = false;

        _gateArrow = arrowGO.GetComponent<RectTransform>();
        _gateArrow.anchorMin = new Vector2(0.5f, 1f);
        _gateArrow.anchorMax = new Vector2(0.5f, 1f);
        _gateArrow.pivot = new Vector2(0.5f, 0.5f);
        _gateArrow.anchoredPosition = new Vector2(0f, -38f);
        _gateArrow.sizeDelta = new Vector2(64f, 64f);

        var instructionPanelGO = new GameObject("GateDirectionInstructionPanel");
        instructionPanelGO.transform.SetParent(_gateArrowGroup.transform, false);
        instructionPanelGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        var instructionPanelRT = instructionPanelGO.GetComponent<RectTransform>();
        instructionPanelRT.anchorMin = new Vector2(0f, 0f);
        instructionPanelRT.anchorMax = new Vector2(1f, 0f);
        instructionPanelRT.pivot = new Vector2(0.5f, 0f);
        instructionPanelRT.anchoredPosition = Vector2.zero;
        instructionPanelRT.sizeDelta = new Vector2(0f, 52f);

        var instructionGO = new GameObject("GateDirectionInstruction");
        instructionGO.transform.SetParent(instructionPanelGO.transform, false);
        _gateArrowInstruction = instructionGO.AddComponent<TextMeshProUGUI>();
        _gateArrowInstruction.text = "Follow the arrow to the blue pillar. Hold Ctrl for short nitro bursts.";
        _gateArrowInstruction.fontSize = 15f;
        _gateArrowInstruction.color = new Color(0.86f, 0.95f, 1f, 0.96f);
        _gateArrowInstruction.alignment = TextAlignmentOptions.Center;
        _gateArrowInstruction.textWrappingMode = TextWrappingModes.Normal;
        _gateArrowInstruction.raycastTarget = false;
        var instructionRT = instructionGO.GetComponent<RectTransform>();
        instructionRT.anchorMin = Vector2.zero;
        instructionRT.anchorMax = Vector2.one;
        instructionRT.pivot = new Vector2(0.5f, 0.5f);
        instructionRT.anchoredPosition = Vector2.zero;
        instructionRT.sizeDelta = Vector2.zero;
        instructionRT.offsetMin = new Vector2(18f, 8f);
        instructionRT.offsetMax = new Vector2(-18f, -8f);
    }

    private void UpdateGateDirectionArrow()
    {
        if (_gateArrowGroup == null || _gateArrow == null || _player == null || _sceneDone) return;

        EscapeGate targetGate = GetNearestGate();
        bool hasTarget = targetGate != null;
        _gateArrowGroup.SetActive(hasTarget);
        if (!hasTarget) return;

        Vector3 playerPosition = _player.transform.position;
        Vector3 gatePosition = targetGate.transform.position;
        playerPosition.y = 0f;
        gatePosition.y = 0f;

        Vector3 forward = _player.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 toGate = gatePosition - playerPosition;
        toGate.Normalize();

        if (forward.sqrMagnitude < 0.001f || toGate.sqrMagnitude < 0.001f) return;

        float signedAngle = Vector3.SignedAngle(forward, toGate, Vector3.up);
        _gateArrow.localRotation = Quaternion.Euler(0f, 0f, -signedAngle);
    }

    private EscapeGate GetNearestGate()
    {
        if (_gates == null || _gates.Length == 0 || _player == null) return null;

        EscapeGate nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;
        Vector3 playerPos = _player.transform.position;

        foreach (EscapeGate gate in _gates)
        {
            if (gate == null) continue;

            float sqrDistance = (gate.transform.position - playerPos).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearest = gate;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearest;
    }

    private Sprite CreateRingSprite()
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool outer = distance <= 45f;
                bool inner = distance <= 32f;
                bool ring = outer && !inner;
                bool glow = distance <= 46f && distance >= 26f;
                float alpha = ring ? 1f : glow ? 0.25f : 0f;
                texture.SetPixel(x, y, alpha > 0f ? new Color(fill.r, fill.g, fill.b, alpha) : transparent);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateArrowSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        int center = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Abs(x - center);
                bool inShaft = y >= 8 && y <= 36 && dx <= 7;
                float headT = Mathf.InverseLerp(28f, 58f, y);
                int headHalfWidth = Mathf.RoundToInt(Mathf.Lerp(28f, 0f, headT));
                bool inHead = y >= 28 && y <= 58 && dx <= headHalfWidth;
                bool innerCut = y >= 8 && y <= 29 && dx <= 2;
                bool shape = (inShaft || inHead) && !innerCut;
                float edgeFade = shape && (dx == headHalfWidth || dx == 7) ? 0.55f : 1f;
                texture.SetPixel(x, y, shape ? new Color(fill.r, fill.g, fill.b, edgeFade) : transparent);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
