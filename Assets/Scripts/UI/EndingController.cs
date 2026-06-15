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
    public GameObject bodyPanel;

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

    [Header("Story Images")]
    public StoryImageDisplay endingImageDisplay;
    public StoryImageLibrary imageLibrary;

    private EndingData _ending;

    void Start()
    {
        // Auto-load StoryImageLibrary from Resources if not wired in Inspector
        if (imageLibrary == null)
            imageLibrary = Resources.Load<StoryImageLibrary>("StoryImageLibrary");

        var propState = ProphecyManager.Instance?.State    ?? new ProphecyState();
        var profile   = GameManager.Instance?.PlayerProfile ?? new PlayerProfile();

        _ending = EndingResolver.Resolve(propState, profile);

        ApplyEndingText();
        ConfigureBeliefChoicePanel();
        ConfigureExitMenu();

        StartCoroutine(RunEndingSequence());
    }

    // ── Text display ────────────────────────────────────────────────────────

    private void ApplyEndingText()
    {
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (bodyText  != null) bodyText.text = _ending.Body;
        if (bodyPanel != null) bodyPanel.SetActive(false);   // shown after image sequence
    }

    // ── Belief-choice panel ─────────────────────────────────────────────────

    private void ConfigureBeliefChoicePanel()
    {
        // Always start hidden — RunEndingSequence shows it at the right time
        if (beliefChoicePanel != null)
            beliefChoicePanel.SetActive(false);

        // Label the buttons
        if (believeButtonLabel != null)
            believeButtonLabel.text = "I now believe wholeheartedly.";
        if (rationalButtonLabel != null)
            rationalButtonLabel.text = "I see it all as possible without God.";

        // Prompt
        if (choicePromptText != null)
            choicePromptText.text = "After experiencing those events yourself — what do you believe now?";

        // Hide result text
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
        if (believeButton  != null) believeButton.interactable  = false;
        if (rationalButton != null) rationalButton.interactable = false;
        // Hide the question panel immediately
        if (beliefChoicePanel != null) beliefChoicePanel.SetActive(false);

        if (choosesFaith)
        {
            // Believe path: go straight to exit menu, no extra images
            if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
        }
        else
        {
            if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
        }
    }

    // ── Ending image sequences ─────────────────────────────────────────────

    private IEnumerator RunEndingSequence()
    {
        bool defended = (_ending.Type != EndingType.Paradox) &&
                        (StateManager.Instance?.Memory?.MadePublicDefence ?? false);
        float holdTime = endingImageDisplay != null ? endingImageDisplay.slideshowHoldTime : 2.5f;

        // Always play ending_0 and ending_1
        if (endingImageDisplay != null && imageLibrary?.endingImages?.Length > 1)
        {
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[0]);
            yield return new WaitForSeconds(holdTime);
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[1]);
            yield return new WaitForSeconds(holdTime);
        }

        if (defended && endingImageDisplay != null && imageLibrary?.endingImages?.Length > 3)
        {
            // Defender path: show ending_2 and ending_3, then exit menu
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[2]);
            yield return new WaitForSeconds(holdTime);
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[3]);
            yield return new WaitForSeconds(holdTime);
            if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
        }
        else if (_ending.RequiresBeliefChoice)
        {
            // Non-defender: reveal body text and the belief-choice panel
            if (bodyPanel != null) { bodyPanel.SetActive(true); ForceBodyPanelLayout(); }
            if (beliefChoicePanel != null) beliefChoicePanel.SetActive(true);
        }
        else
        {
            // Paradox path: ending_2, then ending_3 with the 13th-disciple text overlaid
            if (endingImageDisplay != null && imageLibrary?.endingImages?.Length > 3)
            {
                yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[2]);
                yield return new WaitForSeconds(holdTime);
                yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[3]);
                // Show the bible verse text on top of ending_3
                if (bodyPanel != null) { bodyPanel.SetActive(true); ForceBodyPanelLayout(); }
                yield return new WaitForSeconds(holdTime);
                // Fade image to black; text remains visible
                yield return endingImageDisplay.FadeTo(null);
            }
            else
            {
                if (bodyPanel != null) { bodyPanel.SetActive(true); ForceBodyPanelLayout(); }
            }
            yield return new WaitForSeconds(paradoxExitDelay);
            if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
        }
    }

    private void ForceBodyPanelLayout()
    {
        if (bodyPanel == null) return;
        UnityEngine.Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
            bodyPanel.GetComponent<RectTransform>());
    }

    private IEnumerator ShowEndingImagesAndExit()
    {
        float holdTime = endingImageDisplay != null ? endingImageDisplay.slideshowHoldTime : 2.5f;
        if (endingImageDisplay != null && imageLibrary?.endingImages?.Length > 3)
        {
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[2]);
            yield return new WaitForSeconds(holdTime);
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[3]);
            yield return new WaitForSeconds(holdTime);
        }
        if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
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
