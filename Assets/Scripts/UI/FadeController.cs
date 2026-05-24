using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a full-screen CanvasGroup overlay used for fade-in / fade-out transitions
/// between narrative events.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FadeController : MonoBehaviour
{
    [Range(0.1f, 3f)]
    public float fadeDuration = 0.5f;

    private CanvasGroup _group;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;
    }

    /// <summary>Fade screen to black (alpha 0 → 1). Awaitable via StartCoroutine.</summary>
    public IEnumerator FadeOut()
    {
        _group.blocksRaycasts = true;
        yield return Fade(0f, 1f);
    }

    /// <summary>Fade screen from black (alpha 1 → 0). Awaitable via StartCoroutine.</summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f);
        _group.blocksRaycasts = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        _group.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed      += Time.deltaTime;
            _group.alpha  = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        _group.alpha = to;
    }
}
