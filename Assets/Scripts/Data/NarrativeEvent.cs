using System;
using System.Collections.Generic;

[Serializable]
public class NarrativeEvent
{
    public string id;
    public string title;
    public string text;
    public bool isCanonical;
    public List<EventChoice> choices;
    /// <summary>
    /// Optional subtitle text for each slideshow frame.
    /// Index 0 = frame 1, index 1 = frame 2, etc.
    /// slideshowTexts.Count also determines how many frames go to the slideshow
    /// vs. postChoiceFrames in StoryImageLibrary.
    /// </summary>
    public List<string> slideshowTexts;
}
