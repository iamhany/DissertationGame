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

    // ── Private state ─────────────────────────────────────────────────────────

    private FirstPersonController _player;
    private Vector3               _playerStartPos;

    private JumpPassZone[]    _zones;
    private int               _clearedCount;

    private GuardController[] _guards;
    private Vector3[]         _guardStartPos;
    private Quaternion[]      _guardStartRot;

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
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Additive) return;
        // sceneLoaded fires after OnEnable() but before Update() on the new objects.
        // Destroy() is deferred (end-of-frame), so the duplicate would still run
        // Update() this frame. SetActive(false) takes effect immediately and
        // prevents any further Update/OnEnable calls; Destroy cleans it up later.
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

        if (op != null && op.isDone)
        {
            var demoScene = SceneManager.GetSceneByName(SyntyDemoSceneName);
            foreach (var root in demoScene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    cam.enabled = false;
                foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
                    al.enabled = false;
                // Disable AudioSources to prevent FMOD re-initialisation errors
                // caused by the Demo scene's audio settings conflicting with the
                // already-running FMOD system.
                foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
                    src.enabled = false;
            }
        }

        _player = FindFirstObjectByType<FirstPersonController>();
        if (_player != null) _playerStartPos = _player.transform.position;

        _zones        = FindObjectsByType<JumpPassZone>(FindObjectsSortMode.None);
        _clearedCount = 0;

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
    }

    // ── Public callbacks ──────────────────────────────────────────────────────

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
        bool hasZones = _zones != null && _zones.Length > 0;

        if (instructionText != null)
            instructionText.text = hasZones
                ? "Evade the guards \u00b7 Jump over all barriers to escape"
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
