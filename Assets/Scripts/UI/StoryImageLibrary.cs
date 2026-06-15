using UnityEngine;

/// <summary>
/// ScriptableObject that holds every sprite used in story image sequences.
/// Create via right-click → Create → Game → Story Image Library.
/// Assign this asset to UIManager.imageLibrary and EndingController.imageLibrary in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Game/Story Image Library", fileName = "StoryImageLibrary")]
public class StoryImageLibrary : ScriptableObject
{
    [System.Serializable]
    public struct EventImageSet
    {
        [Tooltip("Must match the event id in the JSON exactly, e.g. \"event_0\", \"event_1\".")]
        public string   eventId;

        [Tooltip("Slideshow images shown before choices appear. eventX_1, eventX_2, …")]
        public Sprite[] slideshowFrames;

        [Tooltip("One entry per choice (index 0 = choice 1). eventX_choice1, eventX_choice2, …")]
        public Sprite[] choiceImages;

        [Tooltip("Shown after the per-choice image, before the next event starts (e.g. event0_3).")]
        public Sprite[] postChoiceFrames;
    }

    [Header("Per-event images")]
    public EventImageSet[] events;

    [Header("Main Menu / Name Entry background")]
    public Sprite mainMenuBackground;

    [Header("Shared — shown as background while choices are on screen")]
    public Sprite choiceBackground;

    [Header("Ending — slot 0-3 map to ending_0 through ending_3")]
    public Sprite[] endingImages;

    /// <summary>Returns the EventImageSet whose eventId matches, or null if not found.</summary>
    public EventImageSet? GetEventSet(string eventId)
    {
        if (events == null) return null;
        foreach (var e in events)
            if (e.eventId == eventId) return e;
        return null;
    }
}
