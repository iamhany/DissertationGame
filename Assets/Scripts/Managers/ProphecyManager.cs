using UnityEngine;

public class ProphecyManager : MonoBehaviour
{
    public static ProphecyManager Instance { get; private set; }

    public ProphecyState State { get; private set; } = new ProphecyState();

    public int Integrity => State.integrity;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ApplyIntegrityDelta(int delta)
    {
        State.integrity = Mathf.Clamp(State.integrity + delta, 0, 100);
    }

    // Returns a SnapbackResult after evaluating the current event. Call after every choice.
    public SnapbackResult CheckSnapback(NarrativeEvent currentEvent)
    {
        var result = SnapbackEngine.Evaluate(State, currentEvent);

        if (result.PlayerAbsorbedIntoHistory)
            State.playerAbsorbedIntoHistory = true;

        return result;
    }

    /// <summary>Resets prophecy state to defaults. Called on game restart.</summary>
    public void ResetState()
    {
        State = new ProphecyState();
    }
}
