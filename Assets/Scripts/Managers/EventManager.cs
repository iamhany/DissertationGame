using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    // Raised when a new event is ready to be displayed
    public event Action<NarrativeEvent, SnapbackResult> OnEventLoaded;
    // Raised when the final event has been resolved
    public event Action OnGameComplete;

    private readonly List<string> _eventSequence = new List<string>
    {
        "event_0", "event_1", "event_2", "event_3", "event_4", "event_5", "event_6", "event_7"
    };

    private int _currentIndex;
    private NarrativeEvent _currentEvent;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Current position in the event sequence — persisted to save file.</summary>
    public int CurrentEventIndex => _currentIndex;

    public void BeginSequence(int startIndex = 0)
    {
        _currentIndex = Mathf.Clamp(startIndex, 0, _eventSequence.Count - 1);
        LoadEvent(_eventSequence[_currentIndex]);
    }

    private void LoadEvent(string eventId)
    {
        TextAsset json = Resources.Load<TextAsset>($"Events/{eventId}");
        if (json == null)
        {
            Debug.LogError($"[EventManager] Could not load event: Events/{eventId}");
            return;
        }

        _currentEvent = JsonUtility.FromJson<NarrativeEvent>(json.text);

        // Ask ProphecyManager whether a snapback applies to this event context
        var snapback = ProphecyManager.Instance.CheckSnapback(_currentEvent);

        OnEventLoaded?.Invoke(_currentEvent, snapback);
    }

    public void OnChoiceMade(EventChoice choice)
    {
        if (choice?.effects != null)
        {
            ProphecyManager.Instance.ApplyIntegrityDelta(choice.effects.prophecyIntegrity);
            StateManager.Instance.ApplyEffects(choice.effects);
        }

        _currentIndex++;

        if (_currentIndex >= _eventSequence.Count)
        {
            // Game complete — wipe the save so Continue is hidden next session
            SaveManager.Instance?.ClearSave();
            OnGameComplete?.Invoke();
            return;
        }

        // Persist progress after every choice
        SaveManager.Instance?.Save();
        LoadEvent(_eventSequence[_currentIndex]);
    }
}
