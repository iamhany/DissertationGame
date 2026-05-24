using UnityEngine;

/// <summary>
/// Selects which flavour of narrative text to surface based on the current
/// prophecy integrity, then substitutes any dynamic tokens (e.g. {playerName}).
/// </summary>
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Returns the display text for the current event, rewritten according to
    /// how far the prophecy has been stressed. If a SnapbackResult provides an
    /// override, that takes precedence.
    /// </summary>
    public string GetDisplayText(NarrativeEvent evt, SnapbackResult snapback)
    {
        string baseText;

        if (snapback.WasTriggered && !string.IsNullOrEmpty(snapback.OverrideText))
        {
            baseText = snapback.OverrideText;
        }
        else
        {
            int integrity = ProphecyManager.Instance.Integrity;

            if (integrity >= 70)
                baseText = evt.text;
            else if (integrity >= 40)
                baseText = AddDistortedFrame(evt.text);
            else
                baseText = AddParadoxFrame(evt.text);
        }

        return ResolveTokens(baseText);
    }

    private string AddDistortedFrame(string text)
    {
        return text + "\n\n[The record shifts. Your presence is felt but not yet named.]";
    }

    private string AddParadoxFrame(string text)
    {
        return text + "\n\n[History strains against your intervention. Something in the account must give.]";
    }

    private string ResolveTokens(string text)
    {
        string playerName = GameManager.Instance?.PlayerProfile?.playerName ?? "Witness";
        return text.Replace("{playerName}", playerName);
    }
}
