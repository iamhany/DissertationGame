using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages the escape-from-Jerusalem sequence.
///
/// Triggered when the player made a public defence of Jesus (event_2 or event_4/5).
/// The guards have your description and are converging. Reach the city gate.
///
/// Unlike the stealth scene this is a hot pursuit — guards move faster,
/// detection starts at 40 %, and the player cannot spend time creeping.
/// Shift is the only way to break line-of-sight.
/// </summary>
public class EscapeSceneManager : MonoBehaviour
{
    public static EscapeSceneManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Scene setup")]
    [Tooltip("How long the player has to reach the exit before the scene auto-fails.")]
    public float escapeTimeLimit = 90f;

    [Header("Detection (starts elevated)")]
    public float startingDetection  = 40f;
    public float detectionDecayRate = 4f;  // slower decay — crowds block guard sight badly

    [Header("UI")]
    public Slider    detectionSlider;
    public Image     detectionFill;
    public Slider    shiftSlider;
    public Image     shiftFill;
    public TMP_Text  timerText;
    public TMP_Text  instructionText;
    public TMP_Text  outcomeText;
    public GameObject outcomePanel;
    public Button    retryButton;
    public Button    continueButton;

    [Header("Colours")]
    public Color safeColor    = new Color(0.2f, 0.8f, 0.3f);
    public Color warningColor = new Color(1f,   0.7f, 0f);
    public Color dangerColor  = new Color(1f,   0.1f, 0.1f);

    // ── Private ───────────────────────────────────────────────────────────────

    private float _detection;
    private float _timeRemaining;
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
        _player        = FindFirstObjectByType<PlayerStealthController>();
        _detection     = startingDetection;
        _timeRemaining = escapeTimeLimit;

        if (detectionSlider != null)
        {
            detectionSlider.minValue = 0f;
            detectionSlider.maxValue = 100f;
        }
        if (shiftSlider != null)
        {
            shiftSlider.minValue = 0f;
            shiftSlider.maxValue = 1f;
        }
        if (outcomePanel != null) outcomePanel.SetActive(false);
        if (retryButton  != null)
        {
            retryButton.gameObject.SetActive(false);
            retryButton.onClick.AddListener(OnRetryClicked);
        }
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(EndScene);
        }

        SetInstructionText();
    }

    void Update()
    {
        if (_sceneDone) return;

        _timeRemaining -= Time.deltaTime;
        _detection     -= detectionDecayRate * Time.deltaTime;
        _detection      = Mathf.Clamp(_detection, 0f, 100f);

        UpdateHUD();

        if (_detection >= 100f)
            StartCoroutine(OnCaptured());
        else if (_timeRemaining <= 0f)
            StartCoroutine(OnTimeOut());
    }

    // ── Called by GuardController ─────────────────────────────────────────────

    public void AddDetection(float amount, Vector3 guardPos)
    {
        if (_sceneDone) return;
        _detection += amount;
    }

    // ── Outcome handlers ──────────────────────────────────────────────────────

    public void OnPlayerEscaped()
    {
        if (_sceneDone) return;
        _sceneDone = true;
        StartCoroutine(OnSuccess());
    }

    private IEnumerator OnSuccess()
    {
        ProphecyManager.Instance?.ApplyIntegrityDelta(+8);
        ShowOutcome(BuildEscapeSuccessText(), showContinue: true);
        yield return null;
    }

    private IEnumerator OnCaptured()
    {
        _sceneDone = true;
        ProphecyManager.Instance?.ApplyIntegrityDelta(-20);

        ShowOutcome(
            "They have you.\n\n" +
            "Two guards pin your arms. A third strikes you across the face with an open hand. " +
            "You taste blood and dust.\n\n" +
            "\"Another one speaking for the Galilean.\" He leans close. \"You will be dealt with after he is.\"\n\n" +
            "They haul you into a side room and bolt the door. " +
            "Hours pass in darkness before they decide you are not worth the trouble of a second trial. " +
            "You are beaten badly and turned out through the eastern gate at dawn.\n\n" +
            "You did not see what happened at Golgotha. But you already knew.",
            allowRetry: true);

        yield return null;
    }

    private IEnumerator OnTimeOut()
    {
        _sceneDone = true;
        ProphecyManager.Instance?.ApplyIntegrityDelta(-8);

        ShowOutcome(
            "You do not make it to the gate before the city tightens around you.\n\n" +
            "The guards are everywhere now. You press yourself into a doorway and wait — for hours — " +
            "until the patrols thin out after dark. By the time you reach the courtyard, " +
            "it is too late to witness anything but the aftermath.",
            allowRetry: true);

        yield return null;
    }

    private void ShowOutcome(string message, bool allowRetry = false, bool showContinue = false)
    {
        if (outcomePanel    != null) outcomePanel.SetActive(true);
        if (outcomeText     != null) outcomeText.text = message;
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (timerText       != null) timerText.gameObject.SetActive(false);

        if (retryButton    != null) retryButton.gameObject.SetActive(allowRetry);
        if (continueButton != null) continueButton.gameObject.SetActive(showContinue);

        foreach (var g in FindObjectsByType<GuardController>(FindObjectsSortMode.None))
            g.enabled = false;
        if (_player != null) _player.enabled = false;
    }

    private void OnRetryClicked()
    {
        // Reload this scene from scratch — managers persist via DontDestroyOnLoad
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void EndScene()
    {
        GameManager.Instance?.ResumeNarrative();
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void UpdateHUD()
    {
        if (detectionSlider != null) detectionSlider.value = _detection;
        if (detectionFill   != null)
        {
            if (_detection >= 70f)
                detectionFill.color = _detection >= 85f ? dangerColor : warningColor;
            else
                detectionFill.color = safeColor;
        }

        if (_player != null)
        {
            if (shiftSlider != null) shiftSlider.value = _player.ShiftCharge;
            if (shiftFill   != null)
                shiftFill.color = _player.IsShifted ? new Color(0.6f, 0.9f, 1f) : Color.white;
        }

        if (timerText != null)
        {
            int mins = Mathf.FloorToInt(_timeRemaining / 60f);
            int secs = Mathf.FloorToInt(_timeRemaining % 60f);
            timerText.text = $"{mins:00}:{secs:00}";
            timerText.color = _timeRemaining < 20f ? dangerColor : Color.white;
        }
    }

    // ── Flavour text ──────────────────────────────────────────────────────────

    private void SetInstructionText()
    {
        if (instructionText == null) return;
        var mem = StateManager.Instance?.Memory;

        if (mem != null && mem.organisedResistance)
            instructionText.text =
                "You organised resistance in the crowd — they saw your face.\n" +
                "The guards are closing in. Reach the city gate before they find you.\n" +
                "[WASD] Move  |  [SHIFT / Q] Phase shift — break line-of-sight";
        else if (mem != null && mem.defendedJesusPublicly)
            instructionText.text =
                "You confronted the Pharisees openly. They have your description.\n" +
                "Escape the city before you are taken.\n" +
                "[WASD] Move  |  [SHIFT / Q] Phase shift — break line-of-sight";
        else
            instructionText.text =
                "Your actions have drawn attention. The guards are hunting you.\n" +
                "Reach the city gate.\n" +
                "[WASD] Move  |  [SHIFT / Q] Phase shift — break line-of-sight";
    }

    private string BuildEscapeSuccessText()
    {
        var mem = StateManager.Instance?.Memory;
        if (mem != null && mem.organisedResistance)
            return "You slip through the Jaffa Gate as the last torchlight sweeps the marketplace behind you.\n\n" +
                   "Somewhere inside the city, the cry goes up for Barabbas.\n\n" +
                   "You changed some minds. Not enough. But the people who heard you — they will not forget it.";
        if (mem != null && mem.defendedJesusPublicly)
            return "You push out through the gate into the open road before the guard patrols converge.\n\n" +
                   "You are bruised. Your hands are shaking.\n\n" +
                   "The scribes who saw you at the Temple have your face in their notes. " +
                   "In another time, that would make you a historical figure. " +
                   "Here it just means the next few days are dangerous.";
        return "You reach the gate. The city exhales behind you.\n\n" +
               "No one follows — not tonight. But they know a stranger was here, " +
               "and that the stranger was not quiet.";
    }
}
