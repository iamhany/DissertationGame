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
    /// Pre-loads the full text (invisible) so ContentSizeFitter can measure the
    /// final box size before any characters are revealed.
    /// </summary>
    public void SetContent(string title, string body)
    {
        _cancelTypewrite = true;   // stop any in-progress Typewrite immediately
        _fullBody = body ?? string.Empty;

        if (narrativeText != null)
        {
            narrativeText.text                 = _fullBody;
            narrativeText.maxVisibleCharacters = 0;
        }

        _canvasGroup.alpha = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            GetComponent<RectTransform>());
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
    /// Box size is already correct from SetContent, so it never resizes during typing.
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

        narrativeText.text                 = text;
        narrativeText.maxVisibleCharacters = 0;

        if (string.IsNullOrEmpty(text)) yield break;

        _cancelTypewrite = false;
        float delay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;
        int total   = narrativeText.textInfo?.characterCount ?? text.Length;

        for (int i = 1; i <= total; i++)
        {
            if (_cancelTypewrite)
            {
                narrativeText.maxVisibleCharacters = int.MaxValue;
                yield break;
            }
            narrativeText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(delay);
        }

        narrativeText.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>
    /// Stops the typewriter immediately and shows the full text.
    /// Call when the player makes a choice before the text finishes.
    /// </summary>
    public void FinishImmediately()
    {
        _cancelTypewrite = true;
        if (narrativeText != null)
            narrativeText.maxVisibleCharacters = int.MaxValue;
    }
}
