using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the Ending scene root.
///
/// For Canon / DistortedWitness paths the player is shown the ending text and
/// then offered two belief-choice buttons ("I believe" / "I rationalise").
/// Clicking either reveals a closing reflection line and locks the buttons.
///
/// For the Paradox path the belief choice is suppressed — the Bible-verse
/// reveal is the complete closing statement.
/// </summary>
public class EndingController : MonoBehaviour
{
    [Header("Main Ending UI")]
    public TMP_Text titleText;
    public TMP_Text bodyText;

    [Header("Belief Choice UI (Canon / DistortedWitness only)")]
    public GameObject   beliefChoicePanel;
    public TMP_Text     choicePromptText;
    public Button       believeButton;
    public TMP_Text     believeButtonLabel;
    public Button       rationalButton;
    public TMP_Text     rationalButtonLabel;

    [Header("Resolution")]
    public TMP_Text choiceResultText;
    [Range(0.1f, 2f)]
    public float resultFadeDuration = 0.8f;

    [Header("Exit Menu")]
    public GameObject exitMenuPanel;
    public TMP_Text   exitQuestionText;
    public Button     restartButton;
    public Button     quitButton;
    [Tooltip("Seconds after Paradox text before exit menu appears.")]
    public float paradoxExitDelay = 5f;

    private EndingData _ending;

    void Start()
    {
        var propState = ProphecyManager.Instance?.State    ?? new ProphecyState();
        var profile   = GameManager.Instance?.PlayerProfile ?? new PlayerProfile();

        _ending = EndingResolver.Resolve(propState, profile);

        ApplyEndingText();
        ConfigureBeliefChoicePanel();
        ConfigureExitMenu();

        // Paradox ending has no belief choice — show exit menu on a timer
        if (!_ending.RequiresBeliefChoice)
            StartCoroutine(ShowExitMenuDelayed(paradoxExitDelay));
    }

    // ── Text display ────────────────────────────────────────────────────────

    private void ApplyEndingText()
    {
        // Title intentionally not shown
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (bodyText  != null) bodyText.text  = _ending.Body;
    }

    // ── Belief-choice panel ─────────────────────────────────────────────────

    private void ConfigureBeliefChoicePanel()
    {
        bool show = _ending.RequiresBeliefChoice;

        if (beliefChoicePanel != null)
            beliefChoicePanel.SetActive(show);

        if (!show) return;

        // Label the buttons
        if (believeButtonLabel != null)
            believeButtonLabel.text = "I now believe wholeheartedly.";
        if (rationalButtonLabel != null)
            rationalButtonLabel.text = "I see it all as possible without God.";

        // Prompt
        if (choicePromptText != null)
            choicePromptText.text = "After experiencing those events yourself — what do you believe now?";

        // Hide result until a choice is made
        if (choiceResultText != null)
        {
            choiceResultText.text = string.Empty;
            choiceResultText.gameObject.SetActive(false);
        }

        // Wire buttons
        if (believeButton != null)
        {
            believeButton.onClick.RemoveAllListeners();
            believeButton.onClick.AddListener(() => OnBeliefChosen(true));
        }

        if (rationalButton != null)
        {
            rationalButton.onClick.RemoveAllListeners();
            rationalButton.onClick.AddListener(() => OnBeliefChosen(false));
        }
    }

    private void OnBeliefChosen(bool choosesFaith)
    {
        // Lock both buttons so the choice cannot be changed
        if (believeButton  != null) believeButton.interactable  = false;
        if (rationalButton != null) rationalButton.interactable = false;

        string resolution = EndingResolver.GetBeliefResolution(choosesFaith);

        if (choiceResultText != null)
        {
            choiceResultText.gameObject.SetActive(true);
            choiceResultText.text = resolution;
            StartCoroutine(FadeInThenShowExit(choiceResultText));
        }
        else
        {
            StartCoroutine(ShowExitMenuDelayed(1.5f));
        }
    }

    // ── Exit menu ────────────────────────────────────────────────────────

    private void ConfigureExitMenu()
    {
        if (exitMenuPanel    != null) exitMenuPanel.SetActive(false);
        if (exitQuestionText != null) exitQuestionText.text = "Are you having second thoughts?";

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator ShowExitMenuDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
    }

    private IEnumerator FadeInThenShowExit(TMP_Text target)
    {
        yield return FadeIn(target);
        yield return new WaitForSeconds(1.5f);
        if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
    }

    // ── Fade helper ─────────────────────────────────────────────────────────

    private IEnumerator FadeIn(TMP_Text target)
    {
        Color c = target.color;
        c.a       = 0f;
        target.color = c;

        float elapsed = 0f;
        while (elapsed < resultFadeDuration)
        {
            elapsed  += Time.deltaTime;
            c.a       = Mathf.Clamp01(elapsed / resultFadeDuration);
            target.color = c;
            yield return null;
        }

        c.a          = 1f;
        target.color = c;
    }
}
