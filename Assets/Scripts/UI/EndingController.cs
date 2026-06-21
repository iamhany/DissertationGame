using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Subtitle")]
    public GameObject subtitlePanel;
    public TMP_Text subtitleText;
    public float subtitleCharsPerSecond = 38f;

    [Header("Paradox Bible Overlay")]
    public GameObject bibleVersePanel;
    public TMP_Text bibleVerseText;

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
    private bool _advanceRequested;

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
        EnsureSubtitlePanel();
        EnsureBibleVerseOverlay();

        StartCoroutine(RunEndingSequence());
    }

    void Update()
    {
        if (Mouse.current?.leftButton.wasPressedThisFrame == true ||
            Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            _advanceRequested = true;
    }

    // ── Text display ────────────────────────────────────────────────────────

    private void ApplyEndingText()
    {
        if (titleText != null) titleText.gameObject.SetActive(false);
        if (bodyText  != null) bodyText.text = _ending.Body;
        if (bodyPanel != null) bodyPanel.SetActive(false);
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
        if (bodyPanel != null) bodyPanel.SetActive(false);
        if (beliefChoicePanel != null) beliefChoicePanel.SetActive(false);
        if (exitMenuPanel != null) exitMenuPanel.SetActive(false);

        if (_ending.Type == EndingType.Paradox)
        {
            yield return RunParadoxSequence();
            yield break;
        }

        yield return ShowImageTextAndWait(1, _ending.Body);
        HideSubtitle();
        if (beliefChoicePanel != null) beliefChoicePanel.SetActive(true);
    }

    private IEnumerator RunParadoxSequence()
    {
        string playerName = GameManager.Instance?.PlayerProfile?.playerName ?? "Witness";

        yield return ShowImageTextAndWait(1, EndingResolver.GetParadoxPresentText());
        yield return ShowImageTextAndWait(2, EndingResolver.GetParadoxVerseDiscoveryText());
        if (endingImageDisplay != null && imageLibrary?.endingImages?.Length > 3)
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[3]);
        HideSubtitle();
        yield return ShowBibleVerseAndWait(EndingResolver.GetParadoxVerseText(playerName));
        yield return ShowSubtitleAndWait(EndingResolver.GetParadoxClosingText());

        HideSubtitle();
        if (exitMenuPanel != null) exitMenuPanel.SetActive(true);
    }

    private IEnumerator ShowImageTextAndWait(int imageIndex, string text)
    {
        if (endingImageDisplay != null && imageLibrary?.endingImages != null &&
            imageIndex >= 0 && imageIndex < imageLibrary.endingImages.Length)
        {
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[imageIndex]);
        }

        yield return ShowSubtitleAndWait(text);
    }

    private IEnumerator ShowSubtitleAndWait(string text)
    {
        if (subtitlePanel == null || subtitleText == null)
            yield break;

        subtitleText.text = string.Empty;
        subtitlePanel.SetActive(true);
        _advanceRequested = false;

        float delay = subtitleCharsPerSecond > 0f ? 1f / subtitleCharsPerSecond : 0f;
        for (int i = 1; i <= text.Length; i++)
        {
            if (_advanceRequested)
            {
                subtitleText.text = text;
                _advanceRequested = false;
                break;
            }

            subtitleText.text = text.Substring(0, i);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            else
                yield return null;
        }

        yield return WaitForAdvance();
    }

    private IEnumerator ShowBibleVerseAndWait(string text)
    {
        if (bibleVersePanel == null || bibleVerseText == null)
        {
            yield return ShowSubtitleAndWait(text);
            yield break;
        }

        bibleVerseText.text = string.Empty;
        bibleVersePanel.SetActive(true);
        _advanceRequested = false;

        bibleVerseText.text = text;
        Color textColor = bibleVerseText.color;
        textColor.a = 0f;
        bibleVerseText.color = textColor;

        float elapsed = 0f;
        while (elapsed < resultFadeDuration)
        {
            if (_advanceRequested)
            {
                textColor.a = 1f;
                bibleVerseText.color = textColor;
                _advanceRequested = false;
                break;
            }

            elapsed += Time.deltaTime;
            textColor.a = Mathf.Clamp01(elapsed / resultFadeDuration);
            bibleVerseText.color = textColor;
            yield return null;
        }

        textColor.a = 1f;
        bibleVerseText.color = textColor;
        yield return WaitForAdvance();
    }

    private IEnumerator WaitForAdvance()
    {
        _advanceRequested = false;
        while (!_advanceRequested)
            yield return null;
        _advanceRequested = false;
    }

    private void HideSubtitle()
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
    }

    private void HideBibleVerse()
    {
        if (bibleVersePanel != null)
            bibleVersePanel.SetActive(false);
    }

    private void EnsureSubtitlePanel()
    {
        if (subtitlePanel != null && subtitleText != null)
        {
            subtitlePanel.SetActive(false);
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        subtitlePanel = new GameObject("EndingSubtitlePanel");
        subtitlePanel.transform.SetParent(canvas.transform, false);
        subtitlePanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        var subtitleRT = subtitlePanel.GetComponent<RectTransform>();
        subtitleRT.anchorMin = new Vector2(0.05f, 0f);
        subtitleRT.anchorMax = new Vector2(0.95f, 0f);
        subtitleRT.pivot = new Vector2(0.5f, 0f);
        subtitleRT.anchoredPosition = new Vector2(0f, 24f);
        subtitleRT.sizeDelta = new Vector2(0f, 0f);

        var fitter = subtitlePanel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = subtitlePanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var textGO = new GameObject("EndingSubtitleText");
        textGO.transform.SetParent(subtitlePanel.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        subtitleText = textGO.AddComponent<TextMeshProUGUI>();
        subtitleText.text = string.Empty;
        subtitleText.fontSize = 20f;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = Color.white;
        subtitleText.textWrappingMode = TextWrappingModes.Normal;
        subtitleText.overflowMode = TextOverflowModes.Overflow;

        subtitlePanel.SetActive(false);
    }

    private void EnsureBibleVerseOverlay()
    {
        if (bibleVersePanel != null && bibleVerseText != null)
        {
            bibleVersePanel.SetActive(false);
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        bibleVersePanel = new GameObject("BibleVerseOverlay");
        bibleVersePanel.transform.SetParent(canvas.transform, false);
        var overlayRT = bibleVersePanel.AddComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0.525f, 0.36f);
        overlayRT.anchorMax = new Vector2(0.665f, 0.61f);
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        bibleVerseText = bibleVersePanel.AddComponent<TextMeshProUGUI>();
        bibleVerseText.text = string.Empty;
        bibleVerseText.fontSize = 18f;
        bibleVerseText.enableAutoSizing = true;
        bibleVerseText.fontSizeMin = 9f;
        bibleVerseText.fontSizeMax = 22f;
        bibleVerseText.alignment = TextAlignmentOptions.Justified;
        bibleVerseText.color = new Color(0.18f, 0.10f, 0.04f, 0.92f);
        bibleVerseText.textWrappingMode = TextWrappingModes.Normal;
        bibleVerseText.overflowMode = TextOverflowModes.Overflow;
        bibleVerseText.richText = true;
        bibleVerseText.raycastTarget = false;

        bibleVersePanel.SetActive(false);
    }

    private IEnumerator ShowEndingImagesAndExit()
    {
        if (endingImageDisplay != null && imageLibrary?.endingImages?.Length > 3)
        {
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[2]);
            yield return WaitForAdvance();
            yield return endingImageDisplay.FadeTo(imageLibrary.endingImages[3]);
            yield return WaitForAdvance();
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
