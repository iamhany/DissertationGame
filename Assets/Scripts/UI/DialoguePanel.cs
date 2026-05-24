using System.Collections;
using TMPro;
using UnityEngine;

public class DialoguePanel : MonoBehaviour
{
    [Header("Text Fields")]
    public TMP_Text titleText;
    public TMP_Text narrativeText;

    [Header("Fade Settings")]
    [Range(0.1f, 2f)]
    public float textFadeDuration = 0.6f;

    private CanvasGroup _canvasGroup;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// Sets text content without triggering the fade animation.
    /// Call this while the screen is still blacked out, then call FadeTextIn().
    /// </summary>
    public void SetContent(string title, string body)
    {
        if (titleText     != null) titleText.text     = title;
        if (narrativeText != null) narrativeText.text = body;
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Legacy helper: sets content and immediately starts the internal fade coroutine.
    /// Prefer SetContent() + FadeTextIn() for sequencing with FadeController.
    /// </summary>
    public void Populate(string title, string body)
    {
        SetContent(title, body);
        StopAllCoroutines();
        StartCoroutine(FadeTextIn());
    }

    /// <summary>Fades the panel alpha from 0 to 1. Awaitable by UIManager.</summary>
    public IEnumerator FadeTextIn()
    {
        _canvasGroup.alpha = 0f;
        float elapsed = 0f;

        while (elapsed < textFadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / textFadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }
}
