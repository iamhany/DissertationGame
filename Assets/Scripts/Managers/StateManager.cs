using UnityEngine;

public class StateManager : MonoBehaviour
{
    public static StateManager Instance { get; private set; }

    public InterpretationState Interpretation { get; private set; } = new InterpretationState();
    public CrowdState          Crowd          { get; private set; } = new CrowdState();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ApplyEffects(ChoiceEffect fx)
    {
        if (fx == null) return;

        Interpretation.faithVsSkepticism += fx.faithVsSkepticism;
        Interpretation.literalVsSymbolic += fx.literalVsSymbolic;
        Interpretation.trustInAuthority  += fx.trustInAuthority;

        Crowd.proJesus  += fx.proJesus;
        Crowd.antiJesus += fx.antiJesus;
        Crowd.neutral    = Mathf.Max(0, Crowd.neutral - Mathf.Abs(fx.proJesus) - Mathf.Abs(fx.antiJesus));
    }

    /// <summary>Resets all state to defaults. Called on game restart.</summary>
    public void ResetState()
    {
        Interpretation = new InterpretationState();
        Crowd          = new CrowdState();
    }
}
