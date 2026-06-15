using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the full-screen Background Image (behind all other UI).
/// UIManager drives transitions through FadeController (sets sprites while blacked out).
/// EndingController uses FadeTo() for its own crossfade sequence.
/// </summary>
[RequireComponent(typeof(Image))]
public class StoryImageDisplay : MonoBehaviour
{
    [Range(0.1f, 2f)] public float crossfadeDuration = 0.6f;
    [Range(0.5f, 8f)] public float slideshowHoldTime  = 2.5f;

    private Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();
        // Start solid black — background stays opaque until a sprite is assigned.
        _image.color = Color.black;
    }

    /// <summary>
    /// Sets the sprite instantly. Call while FadeController has the screen blacked
    /// out so the swap is invisible. Pass null to restore solid black background.
    /// </summary>
    public void SetImmediate(Sprite sprite)
    {
        _image.sprite = sprite;
        _image.color  = sprite != null ? Color.white : Color.black;
    }

    /// <summary>Clears back to solid black immediately.</summary>
    public void Clear()
    {
        _image.sprite = null;
        _image.color  = Color.black;
    }

    /// <summary>
    /// Crossfades to a new sprite using the Image's own alpha.
    /// Used by EndingController (which has no FadeController).
    /// </summary>
    public IEnumerator FadeTo(Sprite sprite)
    {
        // If a sprite is currently showing, fade it out first
        if (_image.sprite != null && _image.color.a > 0.05f)
            yield return FadeAlpha(1f, 0f);

        _image.sprite = sprite;
        _image.color  = new Color(1f, 1f, 1f, 0f);

        if (sprite != null)
            yield return FadeAlpha(0f, 1f);
    }

    private IEnumerator FadeAlpha(float from, float to)
    {
        float elapsed = 0f;
        Color c = _image.color;
        while (elapsed < crossfadeDuration)
        {
            elapsed      += Time.deltaTime;
            c.a           = Mathf.Lerp(from, to, elapsed / crossfadeDuration);
            _image.color  = c;
            yield return null;
        }
        c.a          = to;
        _image.color = c;
    }
}
