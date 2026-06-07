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
        if (choice?.effects != null)
        {
            ProphecyManager.Instance.ApplyIntegrityDelta(choice.effects.prophecyIntegrity);
            StateManager.Instance.ApplyEffects(choice.effects);
        }

        // Record memorable choices into ChoiceMemory
        RecordChoiceMemory(choice);

        _currentIndex++;

        if (_currentIndex >= _eventSequence.Count)
        {
            SaveManager.Instance?.ClearSave();
            OnGameComplete?.Invoke();
            return;
        }

        SaveManager.Instance?.Save();

        // Check whether this advance should launch a gameplay scene instead
        if (ShouldLoadStealthScene())
        {
            ResumeIndex = _currentIndex;
            GameManager.Instance.LoadStealthScene();
            return;
        }

        if (ShouldLoadEscapeScene())
        {
            ResumeIndex = _currentIndex;
            GameManager.Instance.LoadEscapeScene();
            return;
        }

        LoadEvent(_eventSequence[_currentIndex]);
    }

    // ── Choice memory recording ───────────────────────────────────────────────

    private void RecordChoiceMemory(EventChoice choice)
    {
        if (choice == null) return;
        var mem = StateManager.Instance?.Memory;
        if (mem == null) return;

        // We identify choices by their text content so no IDs need adding to JSON
        string t = choice.text ?? "";

        // event_1
        if (t.Contains("clapping and cheering"))             mem.joinedCrowd = true;
        if (t.Contains("speak to one of the disciples"))     mem.warnedDisciplesEarly = true;

        // event_2
        if (t.Contains("warn Jesus that the Pharisees"))     mem.warnedJesusAtTemple = true;
        if (t.Contains("Approach the Pharisees yourself"))   mem.defendedJesusPublicly = true;

        // event_3
        if (t.Contains("Confront Judas directly"))           mem.confrontedJudas = true;
        if (t.Contains("Run to warn Jesus"))                 mem.warnedJesusOfBetrayal = true;

        // event_4
        if (t.Contains("Quietly tell Jesus"))                mem.whisperWarningAtSupper = true;
        if (t.Contains("Speak out to the whole table"))      mem.namedJudasAtTable = true;

        // event_5
        if (t.Contains("Wake the disciples"))                mem.wokeTheDisciples = true;
        if (t.Contains("Step between Judas"))                mem.blockedTheArrest = true;

        // event_6
        if (t.Contains("Shout 'Release Jesus!'"))            mem.shoutedForJesus = true;
        if (t.Contains("Move through the crowd"))            mem.organisedResistance = true;
    }

    // ── Gameplay scene routing ────────────────────────────────────────────────

    /// <summary>
    /// Load the stealth scene before the Garden of Gethsemane (event_5)
    /// when the player has already tried to interfere with Judas.
    /// </summary>
    private bool ShouldLoadStealthScene()
    {
        if (_currentIndex != 5) return false;  // only gate event_5
        var mem = StateManager.Instance?.Memory;
        if (mem == null) return false;
        // If the player confronted Judas or warned Jesus about him, the
        // Temple guards have been told to watch for a suspicious outsider.
        return mem.confrontedJudas || mem.warnedJesusOfBetrayal || mem.namedJudasAtTable;
    }

    /// <summary>
    /// Load the escape scene before Trial Before Pilate (event_6)
    /// when the player made a public defence of Jesus that would be remembered.
    /// </summary>
    private bool ShouldLoadEscapeScene()
    {
        if (_currentIndex != 6) return false;  // only gate event_6
        var mem = StateManager.Instance?.Memory;
        if (mem == null) return false;
        return mem.MadePublicDefence;
    }
}
