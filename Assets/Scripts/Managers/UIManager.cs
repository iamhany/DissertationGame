using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public DialoguePanel   dialoguePanel;
    public FadeController  fadeController;
    public Transform       choiceContainer;   // parent transform for choice buttons
    public GameObject      choiceButtonPrefab;

    [Header("Story Images")]
    public StoryImageDisplay storyImageDisplay;
    public StoryImageLibrary imageLibrary;

    [Header("Subtitle (slideshow captions)")]
    public GameObject         subtitlePanel;
    public TMPro.TMP_Text     subtitleText;

    private readonly List<ChoiceButton> _activeButtons = new List<ChoiceButton>();
    private NarrativeEvent _currentEvent;
    private bool           _skipSlide;
    private Coroutine      _transitionCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // UIManager is scene-specific: do NOT call DontDestroyOnLoad so that
        // Inspector references to scene Canvas objects are always valid.
    }

    void Start()
    {
        // Subscribe here (not OnEnable) so EventManager.Instance is guaranteed
        // to exist — all Awake() calls complete before any Start() runs.
        if (EventManager.Instance == null)
        {
            Debug.LogError("[UIManager] EventManager not found — events won't display.");
            return;
        }

        // Auto-load StoryImageLibrary from Resources if not wired in Inspector
        if (imageLibrary == null)
            imageLibrary = Resources.Load<StoryImageLibrary>("StoryImageLibrary");

        EventManager.Instance.OnEventLoaded  += HandleEventLoaded;
        EventManager.Instance.OnGameComplete += HandleGameComplete;
    }

    void Update()
    {
        if (dialoguePanel != null && dialoguePanel.IsTypewriting &&
            (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true ||
             UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame == true))
        {
            dialoguePanel.FinishImmediately();
            return;
        }

        // Left-click or Space skips the current slideshow hold
        if (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true ||
            UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            _skipSlide = true;
    }

    void OnDisable()
    {
        if (EventManager.Instance == null) return;
        EventManager.Instance.OnEventLoaded  -= HandleEventLoaded;
        EventManager.Instance.OnGameComplete -= HandleGameComplete;
    }

    private void HandleEventLoaded(NarrativeEvent evt, SnapbackResult snapback)
    {
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionToEvent(evt, snapback));
    }

    private IEnumerator TransitionToEvent(NarrativeEvent evt, SnapbackResult snapback)
    {
        _currentEvent = evt;

        if (fadeController == null)
        {
            Debug.LogError("[UIManager] fadeController is not assigned.");
            yield break;
        }
        if (dialoguePanel == null)
        {
            Debug.LogError("[UIManager] dialoguePanel is not assigned.");
            yield break;
        }

        // Screen goes black while we swap content
        yield return fadeController.FadeOut();

        ClearChoiceButtons();
        if (choiceContainer != null) choiceContainer.gameObject.SetActive(true);
        if (dialoguePanel   != null) dialoguePanel.gameObject.SetActive(true);

        string displayText = NarrativeManager.Instance.GetDisplayText(evt, snapback);
        dialoguePanel.SetContent(evt.title, displayText);

        // ── Slideshow ─────────────────────────────────────────────────────────────
        // Each frame: swap image+subtitle while blacked out, FadeIn to reveal,
        // show full subtitle instantly, wait for click or hold-time, FadeOut.
        if (storyImageDisplay != null && imageLibrary != null)
        {
            var evtSet = imageLibrary.GetEventSet(evt.id);
            if (evtSet.HasValue && evtSet.Value.slideshowFrames?.Length > 0)
            {
                var frames = evtSet.Value.slideshowFrames;
                for (int i = 0; i < frames.Length; i++)
                {
                    string frameText = (evt.slideshowTexts != null && i < evt.slideshowTexts.Count)
                        ? evt.slideshowTexts[i] : null;

                    storyImageDisplay.SetImmediate(frames[i]);
                    ShowSubtitle(null);                      // hide while blacked out
                    yield return fadeController.FadeIn();
                    if (!string.IsNullOrEmpty(frameText))
                        yield return TypewriteSubtitle(frameText);
                    yield return WaitForClickOrTimeout(storyImageDisplay.slideshowHoldTime);
                    ShowSubtitle(null);
                    yield return fadeController.FadeOut();
                }
            }

            storyImageDisplay.SetImmediate(imageLibrary.choiceBackground);
        }

        // dialoguePanel.SetContent(evt.title, displayText) was called above with
        // evt.text ("Do you go?") — no need to touch it again.
        SpawnChoiceButtons(evt);
        SetChoiceButtonsInteractable(false);

        yield return fadeController.FadeIn();
        yield return dialoguePanel.FadeTextIn();
        SetChoiceButtonsInteractable(true);
    }

    private void SpawnChoiceButtons(NarrativeEvent evt)
    {
        if (evt.choices == null) return;

        for (int i = 0; i < evt.choices.Count; i++)
        {
            var choice = evt.choices[i];
            int index  = i;
            GameObject go = Instantiate(choiceButtonPrefab, choiceContainer);
            var btn = go.GetComponent<ChoiceButton>();
            if (btn == null)
            {
                Debug.LogError("[UIManager] choiceButtonPrefab is missing a ChoiceButton component.");
                continue;
            }
            btn.Init(choice, c => StartCoroutine(ShowChoiceImageThenProceed(c, index)));
            _activeButtons.Add(btn);
        }
    }

    private IEnumerator ShowChoiceImageThenProceed(EventChoice choice, int choiceIndex)
    {
        dialoguePanel?.FinishImmediately();
        if (dialoguePanel != null) dialoguePanel.gameObject.SetActive(false);

        if (choiceContainer != null)
            choiceContainer.gameObject.SetActive(false);

        if (storyImageDisplay != null && imageLibrary != null && _currentEvent != null)
        {
            var evtSet = imageLibrary.GetEventSet(_currentEvent.id);
            if (evtSet.HasValue)
            {
                // 1. Per-choice image (e.g. event0_choice1)
                bool hasChoiceImg = evtSet.Value.choiceImages != null
                    && choiceIndex < evtSet.Value.choiceImages.Length
                    && evtSet.Value.choiceImages[choiceIndex] != null;
                if (hasChoiceImg)
                {
                    yield return fadeController.FadeOut();
                    storyImageDisplay.SetImmediate(evtSet.Value.choiceImages[choiceIndex]);
                    yield return fadeController.FadeIn();
                    yield return WaitForClickOrTimeout(1.5f);
                }

                // 2. Post-choice transition frames (e.g. event0_3 shown before event_1)
                if (evtSet.Value.postChoiceFrames?.Length > 0)
                {
                    foreach (var frame in evtSet.Value.postChoiceFrames)
                    {
                        yield return fadeController.FadeOut();
                        storyImageDisplay.SetImmediate(frame);
                        yield return fadeController.FadeIn();
                        yield return WaitForClickOrTimeout(storyImageDisplay.slideshowHoldTime);
                    }
                }
            }
        }

        EventManager.Instance.OnChoiceMade(choice, choiceIndex);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ShowSubtitle(string text)
    {
        if (subtitlePanel == null) return;
        bool show = !string.IsNullOrEmpty(text);
        if (show && subtitleText != null) subtitleText.text = text;
        subtitlePanel.SetActive(show);
    }

    private IEnumerator TypewriteSubtitle(string text)
    {
        if (subtitlePanel == null || subtitleText == null || string.IsNullOrEmpty(text))
            yield break;

        subtitleText.text = string.Empty;
        subtitlePanel.SetActive(true);
        _skipSlide = false;

        float charsPerSec = dialoguePanel != null ? dialoguePanel.charsPerSecond : 38f;
        float delay = charsPerSec > 0f ? 1f / charsPerSec : 0f;

        for (int i = 1; i <= text.Length; i++)
        {
            if (_skipSlide)
            {
                subtitleText.text = text;   // snap to full on click
                _skipSlide = false;
                yield break;
            }
            subtitleText.text = text.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator WaitForClickOrTimeout(float seconds)
    {
        float elapsed = 0f;
        _skipSlide = false;
        while (elapsed < seconds && !_skipSlide)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _skipSlide = false;
    }

    private void ClearChoiceButtons()
    {
        foreach (var btn in _activeButtons)
            if (btn != null) Destroy(btn.gameObject);
        _activeButtons.Clear();
    }

    private void SetChoiceButtonsInteractable(bool interactable)
    {
        foreach (var btn in _activeButtons)
        {
            if (btn == null) continue;
            var button = btn.GetComponent<UnityEngine.UI.Button>();
            if (button != null) button.interactable = interactable;
        }
    }

    private void HandleGameComplete()
    {
        StartCoroutine(EndingTransition());
    }

    private IEnumerator EndingTransition()
    {
        if (fadeController != null)
            yield return fadeController.FadeOut();
        GameManager.Instance.TriggerEnding();
    }
}
