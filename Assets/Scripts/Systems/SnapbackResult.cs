/// <summary>
/// Immutable result returned by SnapbackEngine.Evaluate().
/// </summary>
public class SnapbackResult
{
    public bool   WasTriggered             { get; }
    public string OverrideText             { get; }
    public string SubstituteCharacter      { get; }
    public bool   PlayerAbsorbedIntoHistory { get; }

    public static readonly SnapbackResult None = new SnapbackResult(false, null, null, false);

    public SnapbackResult(bool triggered, string overrideText,
                          string substituteCharacter, bool absorbed)
    {
        WasTriggered              = triggered;
        OverrideText              = overrideText;
        SubstituteCharacter       = substituteCharacter;
        PlayerAbsorbedIntoHistory = absorbed;
    }
}
