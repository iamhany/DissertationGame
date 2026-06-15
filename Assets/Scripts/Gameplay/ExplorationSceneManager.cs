using System.Collections;
using TMPro;
using UnityEngine;
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

    private JumpPassZone[]    _zones;
    private int               _clearedCount;

    private GuardController[] _guards;
    private Vector3[]         _guardStartPos;
    private Quaternion[]      _guardStartRot;

    private EscapeGate[]      _gates;

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

        // Camera / AudioListener / AudioSource suppression already handled in OnSceneLoaded.

        _player = FindFirstObjectByType<FirstPersonController>();
        if (_player != null) _playerStartPos = _player.transform.position;

        _zones        = FindObjectsByType<JumpPassZone>(FindObjectsSortMode.None);
        _clearedCount = 0;

        _gates = FindObjectsByType<EscapeGate>(FindObjectsSortMode.None);

        _guards        = FindObjectsByType<GuardController>(FindObjectsSortMode.None);
        _guardStartPos = new Vector3[_guards.Length];
        _guardStartRot = new Quaternion[_guards.Length];
        for (int i = 0; i < _guards.Length; i++)
        {
            _guardStartPos[i] = _guards[i].transform.position;
            _guardStartRot[i] = _guards[i].transform.rotation;
        }

        if (continuePanel != null) continuePanel.SetActive(false);
        if (retryPanel    != null) retryPanel.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (retryButton    != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (skipButton     != null) skipButton.onClick.AddListener(() => TriggerSuccess("[SKIP] Level bypassed."));

        RefreshUI();
    }

    void Update()
    {
        if (_sceneDone) return;
        // Fallback shortcut when there are no zones (free-roam mode)
        if (_zones != null && _zones.Length == 0 &&
            UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true)
        {
            TriggerSuccess("You have witnessed the Garden.\nReturn to the story?");
        }
        // Q = skip level (testing shortcut, mirrors the skip button)
        if (UnityEngine.InputSystem.Keyboard.current?.qKey.wasPressedThisFrame == true)
        {
            TriggerSuccess("[SKIP] Level bypassed.");
        }
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
        foreach (var g in _guards) g.enabled = false;
        if (retryPanel != null) retryPanel.SetActive(true);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void TriggerSuccess(string message)
    {
        _sceneDone = true;
        _player?.LockCursor(false);
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (progressText    != null) progressText.gameObject.SetActive(false);
        if (continuePanelMessage != null) continuePanelMessage.text = message;
        if (continuePanel != null) continuePanel.SetActive(true);
    }

    private void RefreshUI()
    {
        bool hasGate  = _gates  != null && _gates.Length  > 0;
        bool hasZones = _zones  != null && _zones.Length  > 0;

        if (instructionText != null)
            instructionText.text = (hasGate || hasZones)
                ? "Evade the guards \u00b7 Reach the blue pillar to escape"
                : "Explore the Garden of Gethsemane\nWASD \u00b7 Mouse to look \u00b7 [E] to finish";

        if (progressText != null)
            progressText.text = hasZones
                ? $"Barriers: {_clearedCount} / {_zones.Length}"
                : string.Empty;
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
            _guards[i].transform.SetPositionAndRotation(_guardStartPos[i], _guardStartRot[i]);
            _guards[i].ResetGuard();
            _guards[i].enabled = true;
        }

        if (_player != null) _player.transform.position = _playerStartPos;
        _player?.LockCursor(true);

        if (retryPanel      != null) retryPanel.SetActive(false);
        if (instructionText != null) instructionText.gameObject.SetActive(true);
        if (progressText    != null) progressText.gameObject.SetActive(true);
        RefreshUI();
    }
}
