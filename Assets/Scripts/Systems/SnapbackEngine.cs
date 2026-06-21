/// <summary>
/// Stateless engine that enforces prophetic convergence.
/// Called after every player choice; returns a SnapbackResult describing how
/// the timeline corrects itself so canonical events still occur.
/// </summary>
public static class SnapbackEngine
{
    // Integrity thresholds
    private const int DistortedThreshold = 50;
    private const int SubstitutionThreshold = 30;
    private const int AbsorptionThreshold = 10;

    // Characters who can inherit a canonical role if the primary agent is disrupted
    private static readonly string[] SubstituteAgents =
    {
        "a Pharisee who had been watching from the shadows",
        "one of the temple guards who knew the garden well",
        "a merchant who owed a debt to the high priest"
    };

    public static SnapbackResult Evaluate(ProphecyState state, NarrativeEvent evt)
    {
        if (state == null || evt == null)
            return SnapbackResult.None;

        int integrity = state.integrity;

        // Full integrity — no correction needed
        if (integrity >= DistortedThreshold)
            return SnapbackResult.None;

        // Heavy intervention — player is absorbed into historical record
        if (integrity < AbsorptionThreshold)
        {
            string absorbText = BuildAbsorptionText(evt);
            return new SnapbackResult(true, absorbText, null, true);
        }

        // Moderate intervention — role substitution: someone else fulfils the canonical role
        if (integrity < SubstitutionThreshold)
        {
            string substitute = PickSubstitute(integrity);
            string subText = BuildSubstitutionText(evt, substitute);
            return new SnapbackResult(true, subText, substitute, false);
        }

        // Mild intervention — narrative reinterpretation only, no text override
        return new SnapbackResult(true, null, null, false);
    }

    private static string PickSubstitute(int integrity)
    {
        int index = ((100 - integrity) / 10) % SubstituteAgents.Length;
        return SubstituteAgents[index];
    }

    private static string BuildSubstitutionText(NarrativeEvent evt, string substitute)
    {
        return $"{evt.text}\n\n" +
               $"<i>Your intervention was felt — but the prophecy found another way. " +
               $"The role once held by Judas was now carried out by {substitute}. " +
               $"The outcome remained the same.</i>";
    }

    private static string BuildAbsorptionText(NarrativeEvent evt)
    {
        return $"{evt.text}\n\n" +
               "<i>The timeline strains under the weight of your presence. " +
               "You have pushed against prophecy so forcefully that history " +
               "can no longer ignore you. It will find a way to account for you.</i>";
    }
}
