using System.Collections;
using TMPro;
using UnityEngine;

public class DialoguePanel : MonoBehaviour
{
    [Header("Text Fields")]
    public TMP_Text titleText;     // kept for legacy wiring; hidden at runtime
    public TMP_Text narrativeText;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second.")]
    public float charsPerSecond = 38f;

    private CanvasGroup _canvasGroup;
    private Coroutine   _typewriterCoroutine;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Title is never shown
        if (titleText != null) titleText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Sets text content without animation (called while screen is blacked out).
    /// </summary>
    public void SetContent(string title, string body)
    {
        // title param intentionally ignored — player asked for no titles
        if (narrativeText != null) narrativeText.text = string.Empty;
        _fullBody = body;
        _canvasGroup.alpha = 0f;
    }

    private string _fullBody = string.Empty;

    /// <summary>Legacy helper.</summary>
    public void Populate(string title, string body)
    {
        SetContent(title, body);
        StopAllCoroutines();
        StartCoroutine(FadeTextIn());
    }

    /// <summary>
    /// Fades the panel in then reveals text character-by-character at read speed.
    /// Awaitable by UIManager.
    /// </summary>
    public IEnumerator FadeTextIn()
    {
        // Instant fade-in (panel becomes visible with empty text)
        _canvasGroup.alpha = 1f;

        if (narrativeText == null) yield break;

        // Stop any previous typewriter
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _typewriterCoroutine = StartCoroutine(Typewrite(_fullBody));
        yield return _typewriterCoroutine;
    }

    private IEnumerator Typewrite(string text)
    {
        narrativeText.text = string.Empty;
        if (string.IsNullOrEmpty(text)) yield break;

        float delay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;
        int i = 0;
        while (i < text.Length)
        {
            // Reveal one more character; use TMP rich-text-safe approach
            i++;
            narrativeText.text = text.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }
    }
}
