using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialoguePanel : MonoBehaviour
{
    [Header("Text Fields")]
    public TMP_Text titleText;     // kept for legacy wiring; hidden at runtime
    public TMP_Text narrativeText;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second.")]
    public float charsPerSecond = 38f;

    private CanvasGroup _canvasGroup;
    private bool         _cancelTypewrite;
    private bool         _isTypewriting;
    private RectTransform _rectTransform;

    public bool IsTypewriting => _isTypewriting;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rectTransform = GetComponent<RectTransform>();

        // Title is never shown
        if (titleText != null) titleText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Sets text content without animation (called while screen is blacked out).
    /// Starts with an empty body so the ContentSizeFitter can grow the box as
    /// the typewriter reveals text.
    /// </summary>
    public void SetContent(string title, string body)
    {
        _cancelTypewrite = true;   // stop any in-progress Typewrite immediately
        _isTypewriting = false;
        _fullBody = body ?? string.Empty;

        if (narrativeText != null)
        {
            narrativeText.text                 = string.Empty;
            narrativeText.maxVisibleCharacters = int.MaxValue;
        }

        _canvasGroup.alpha = 0f;

        RebuildLayout();
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
    /// Makes the panel visible then reveals text character-by-character.
    /// The panel grows as the visible text grows.
    /// </summary>
    public IEnumerator FadeTextIn()
    {
        _canvasGroup.alpha = 1f;

        if (narrativeText == null) yield break;

        // Yield the enumerator directly — no StartCoroutine on this object,
        // so it works even if DialoguePanel was previously inactive.
        yield return Typewrite(_fullBody);
    }

    private IEnumerator Typewrite(string text)
    {
        if (narrativeText == null) yield break;

        narrativeText.text                 = string.Empty;
        narrativeText.maxVisibleCharacters = int.MaxValue;
        RebuildLayout();

        if (string.IsNullOrEmpty(text)) yield break;

        _cancelTypewrite = false;
        _isTypewriting = true;
        float delay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;

        for (int i = 1; i <= text.Length; i++)
        {
            if (_cancelTypewrite)
            {
                narrativeText.text = text;
                RebuildLayout();
                _isTypewriting = false;
                yield break;
            }

            narrativeText.text = text.Substring(0, i);
            RebuildLayout();

            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            else
                yield return null;
        }

        narrativeText.text = text;
        RebuildLayout();
        _isTypewriting = false;
    }

    /// <summary>
    /// Stops the typewriter immediately and shows the full text.
    /// Call when the player makes a choice before the text finishes.
    /// </summary>
    public void FinishImmediately()
    {
        _cancelTypewrite = true;
        if (narrativeText != null)
        {
            narrativeText.text = _fullBody;
            narrativeText.maxVisibleCharacters = int.MaxValue;
            RebuildLayout();
        }
        _isTypewriting = false;
    }

    private void RebuildLayout()
    {
        if (_rectTransform == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
    }
}
