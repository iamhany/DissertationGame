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

    // The event ID that was active when a gameplay scene was launched,
    // so we can resume at the right index after returning.
    public int ResumeIndex { get; set; }

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
        OnChoiceMade(choice, -1);
    }

    public void OnChoiceMade(EventChoice choice, int choiceIndex)
    {
        if (choice?.effects != null)
        {
            ProphecyManager.Instance.ApplyIntegrityDelta(choice.effects.prophecyIntegrity);
            StateManager.Instance.ApplyEffects(choice.effects);
        }

        // Record memorable choices into ChoiceMemory
        RecordChoiceMemory(choice, choiceIndex);

        _currentIndex++;

        if (_currentIndex >= _eventSequence.Count)
        {
            SaveManager.Instance?.ClearSave();
            OnGameComplete?.Invoke();
            return;
        }

        if (ShouldLoadEscapeScene())
        {
            StateManager.Instance.Memory.escapeScenePlayed = true;
            if (StateManager.Instance.Memory.lastChoiceWasSecondOption)
                StateManager.Instance.Memory.secondOptionChoiceCountSinceBoldest = 0;
            SaveManager.Instance?.Save();
            ResumeIndex = _currentIndex;
            GameManager.Instance.LoadExplorationScene();
            return;
        }

        SaveManager.Instance?.Save();
        LoadEvent(_eventSequence[_currentIndex]);
    }

    // ── Choice memory recording ───────────────────────────────────────────────

    private void RecordChoiceMemory(EventChoice choice, int choiceIndex)
    {
        if (choice == null) return;
        var mem = StateManager.Instance?.Memory;
        if (mem == null) return;

        // We identify choices by their text content so no IDs need adding to JSON
        string t = choice.text ?? "";

        mem.lastChoiceEventId = _currentEvent?.id;
        mem.lastChoiceKey = null;
        mem.lastChoiceWasSecondOption = choiceIndex == 1;
        mem.lastChoiceWasBoldestOption = choiceIndex == 2;
        if (mem.lastChoiceWasSecondOption)
        {
            mem.hasChosenSecondOption = true;
            mem.secondOptionChoiceCountSinceBoldest++;
        }
        if (mem.lastChoiceWasBoldestOption)
        {
            mem.boldestOptionChoiceCount++;
            mem.secondOptionChoiceCountSinceBoldest = 0;
        }

        // event_1
        if (t.Contains("clapping and cheering"))             { mem.joinedCrowd = true; mem.lastChoiceKey = ChoiceMemory.JoinCrowd; }
        if (t.Contains("speak to one of the disciples"))     { mem.warnedDisciplesEarly = true; mem.lastChoiceKey = ChoiceMemory.WarnDisciplesEarly; }

        // event_2
        if (t.Contains("warn Jesus that the Pharisees"))     { mem.warnedJesusAtTemple = true; mem.lastChoiceKey = ChoiceMemory.WarnJesusAtTemple; }
        if (t.Contains("Approach the Pharisees yourself"))   { mem.defendedJesusPublicly = true; mem.lastChoiceKey = ChoiceMemory.DefendJesusPublicly; }

        // event_3
        if (t.Contains("Confront Judas directly"))           { mem.confrontedJudas = true; mem.lastChoiceKey = ChoiceMemory.ConfrontJudas; }
        if (t.Contains("Run to warn Jesus"))                 { mem.warnedJesusOfBetrayal = true; mem.lastChoiceKey = ChoiceMemory.WarnJesusOfBetrayal; }

        // event_4
        if (t.Contains("Quietly tell Jesus"))                { mem.whisperWarningAtSupper = true; mem.lastChoiceKey = ChoiceMemory.WhisperWarningAtSupper; }
        if (t.Contains("Speak out to the whole table"))      { mem.namedJudasAtTable = true; mem.lastChoiceKey = ChoiceMemory.NameJudasAtTable; }

        // event_5
        if (t.Contains("Wake the disciples"))                { mem.wokeTheDisciples = true; mem.lastChoiceKey = ChoiceMemory.WakeDisciples; }
        if (t.Contains("Step between Judas"))                { mem.blockedTheArrest = true; mem.lastChoiceKey = ChoiceMemory.BlockArrest; }

        // event_6
        if (t.Contains("Shout 'Release Jesus!'"))            { mem.shoutedForJesus = true; mem.lastChoiceKey = ChoiceMemory.ShoutForJesus; }
        if (t.Contains("Move through the crowd"))            { mem.organisedResistance = true; mem.lastChoiceKey = ChoiceMemory.OrganiseResistance; }
    }

    // ── Gameplay scene routing ────────────────────────────────────────────────

    /// <summary>
    /// Load the guard escape scene for option 3 choices after the first bold
    /// intervention. A very public defence can trigger it immediately.
    /// </summary>
    private bool ShouldLoadEscapeScene()
    {
        var mem = StateManager.Instance?.Memory;
        if (mem == null) return false;
        return mem.ShouldEscapeAfterLastChoice;
    }
}
