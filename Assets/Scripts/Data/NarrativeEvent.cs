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
}
