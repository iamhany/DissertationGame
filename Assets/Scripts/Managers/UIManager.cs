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

    private readonly List<ChoiceButton> _activeButtons = new List<ChoiceButton>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // UIManager is scene-specific: do NOT call DontDestroyOnLoad so that
        // Inspector references to scene Canvas objects are always valid.
    }

    void OnEnable()
    {
        // Guard against direct scene-play in the Editor where DontDestroyOnLoad
        // managers have not been initialised yet.
        if (EventManager.Instance == null) return;

        EventManager.Instance.OnEventLoaded  += HandleEventLoaded;
        EventManager.Instance.OnGameComplete += HandleGameComplete;
    }

    void OnDisable()
    {
        if (EventManager.Instance == null) return;
        EventManager.Instance.OnEventLoaded  -= HandleEventLoaded;
        EventManager.Instance.OnGameComplete -= HandleGameComplete;
    }

    private void HandleEventLoaded(NarrativeEvent evt, SnapbackResult snapback)
    {
        StartCoroutine(TransitionToEvent(evt, snapback));
    }

    private IEnumerator TransitionToEvent(NarrativeEvent evt, SnapbackResult snapback)
    {
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

        yield return fadeController.FadeOut();

        ClearChoiceButtons();

        string displayText = NarrativeManager.Instance.GetDisplayText(evt, snapback);

        // Set text content while screen is blacked out
        dialoguePanel.SetContent(evt.title, displayText);
        SpawnChoiceButtons(evt);

        // Reveal screen and fade in text simultaneously
        yield return fadeController.FadeIn();
        yield return dialoguePanel.FadeTextIn();
    }

    private void SpawnChoiceButtons(NarrativeEvent evt)
    {
        if (evt.choices == null) return;

        foreach (var choice in evt.choices)
        {
            GameObject go = Instantiate(choiceButtonPrefab, choiceContainer);
            var btn = go.GetComponent<ChoiceButton>();
            if (btn == null)
            {
                Debug.LogError("[UIManager] choiceButtonPrefab is missing a ChoiceButton component.");
                continue;
            }
            btn.Init(choice, EventManager.Instance.OnChoiceMade);
            _activeButtons.Add(btn);
        }
    }

    private void ClearChoiceButtons()
    {
        foreach (var btn in _activeButtons)
            if (btn != null) Destroy(btn.gameObject);
        _activeButtons.Clear();
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
