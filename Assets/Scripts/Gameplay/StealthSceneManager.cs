using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Garden of Gethsemane stealth challenge.
///
/// Objective  : reach the Exit marker without being fully detected.
/// Detection  : fills from GuardController sense checks.
///              At 100 % → caught → narrative penalty applied, scene ends.
/// Success    : player reaches the Exit → bonus applied, narrative scene resumes.
/// </summary>
public class StealthSceneManager : MonoBehaviour
{
    public static StealthSceneManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Detection falls this many units/sec while guards cannot sense player.")]
    public float detectionDecayRate = 8f;
    [Tooltip("Detection above this threshold turns the meter red.")]
    public float alertThreshold     = 60f;

    [Header("UI")]
    public Slider    detectionSlider;
    public Image     detectionFill;
    public TMP_Text  instructionText;
    public TMP_Text  outcomeText;
    public GameObject outcomePanel;
    public Button    continueButton;

    [Header("Colours")]
    public Color safeColor    = new Color(0.2f, 0.8f, 0.3f);
    public Color warningColor = new Color(1f,   0.7f, 0f);
    public Color dangerColor  = new Color(1f,   0.1f, 0.1f);

    [Header("Shift HUD")]
    public Slider    shiftSlider;
    public Image     shiftFill;
    public TMP_Text  shiftLabel;

    // ── Private ───────────────────────────────────────────────────────────────

    private float _detection;          // 0-100
    private bool  _sceneDone;
    private PlayerStealthController _player;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _player = FindFirstObjectByType<PlayerStealthController>();

        if (detectionSlider != null)
        {
            detectionSlider.minValue = 0f;
            detectionSlider.maxValue = 100f;
        }

        if (outcomePanel != null) outcomePanel.SetActive(false);
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(EndScene);
        }

        if (instructionText != null)
        {
            var mem = StateManager.Instance?.Memory;
            if (mem != null && mem.blockedTheArrest)
                instructionText.text = "The guards know your face.\nReach the far side of the garden unseen.\n[WASD] Move  |  [SHIFT / Q] Phase shift — silences footsteps, shrinks guard vision";
            else if (mem != null && mem.confrontedJudas)
                instructionText.text = "Judas told the guards to watch for a stranger.\nSlip through the garden without being caught.\n[WASD] Move  |  [SHIFT / Q] Phase shift — silences footsteps, shrinks guard vision";
            else
                instructionText.text = "Navigate the Garden of Gethsemane without alerting the Temple guards.\n[WASD] Move  |  [SHIFT / Q] Phase shift — silences footsteps, shrinks guard vision";
        }
    }

    void Update()
    {
        if (_sceneDone) return;

        // Decay detection when no guard is actively sensing the player
        _detection -= detectionDecayRate * Time.deltaTime;
        _detection  = Mathf.Clamp(_detection, 0f, 100f);

        UpdateDetectionHUD();
        UpdateShiftHUD();

        if (_detection >= 100f)
            StartCoroutine(OnCaught());
    }

    // ── Called by GuardController ─────────────────────────────────────────────

    /// <summary>Increase detection meter. guardPos used for future audio cues.</summary>
    public void AddDetection(float amount, Vector3 guardPos)
    {
        if (_sceneDone) return;
        _detection += amount;
    }

    // ── Outcome handlers ──────────────────────────────────────────────────────

    public void OnPlayerReachedExit()
    {
        if (_sceneDone) return;
        _sceneDone = true;
        StartCoroutine(OnSuccess());
    }

    private IEnumerator OnSuccess()
    {
        // Reward: small prophecy integrity bonus for clever restraint
        ProphecyManager.Instance?.ApplyIntegrityDelta(+5);

        ShowOutcome(
            "You slip through the garden like a ghost.\n\nThe guards pass within metres of you. " +
            "The torchlight sweeps the olive trees but does not find you.\n\nAhead, Jesus kneels in prayer. " +
            "You crouch in the dark and watch. And wait. History moves forward — with you inside it.");

        yield return null;
    }

    private IEnumerator OnCaught()
    {
        _sceneDone = true;

        // Penalty: being caught and interrogated damages prophecy integrity
        ProphecyManager.Instance?.ApplyIntegrityDelta(-15);

        ShowOutcome(
            "A guard's torch finds your face.\n\n\"You — stop!\"\n\n" +
            "Rough hands drag you backward. They speak quickly in Aramaic. " +
            "You do not understand every word, but you understand the tone.\n\n" +
            "They interrogate you for what feels like an hour, then throw you out through the eastern gate. " +
            "By the time you return to the garden, it is over. Judas has already given the sign.");

        yield return null;
    }

    private void ShowOutcome(string message)
    {
        if (outcomePanel    != null) outcomePanel.SetActive(true);
        if (outcomeText     != null) outcomeText.text = message;
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (continueButton  != null) continueButton.gameObject.SetActive(true);

        foreach (var g in FindObjectsByType<GuardController>(FindObjectsSortMode.None))
            g.enabled = false;
        if (_player != null) _player.enabled = false;
    }

    private void EndScene()
    {
        GameManager.Instance?.ResumeNarrative();
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void UpdateDetectionHUD()
    {
        if (detectionSlider != null) detectionSlider.value = _detection;
        if (detectionFill   != null)
        {
            if (_detection >= alertThreshold)
                detectionFill.color = _detection >= 80f ? dangerColor : warningColor;
            else
                detectionFill.color = safeColor;
        }
    }

    private void UpdateShiftHUD()
    {
        if (_player == null) return;
        if (shiftSlider != null) shiftSlider.value = _player.ShiftCharge;
        if (shiftFill   != null)
            shiftFill.color = _player.IsShifted ? new Color(0.6f, 0.9f, 1f) : Color.white;
        if (shiftLabel  != null)
            shiftLabel.text = _player.IsShifted ? "SHIFTED" : "SHIFT";
    }
}
