public enum EndingType
{
    Canon,
    DistortedWitness,
    Paradox
}

public struct EndingData
{
    public EndingType Type;
    public string     Title;
    public string     Body;
    /// <summary>
    /// True for Canon and DistortedWitness paths: the player must choose
    /// whether they now believe or have rationalised away their faith.
    /// False for the Paradox path, which has its own closed ending.
    /// </summary>
    public bool RequiresBeliefChoice;
}

/// <summary>
/// Resolves which of the three endings the player receives based on how much
/// they degraded prophecy integrity, injects the player's chosen name into the
/// Paradox ending, and supplies post-journey belief resolution text.
/// </summary>
public static class EndingResolver
{
    private const int CanonThreshold   = 70;
    private const int ParadoxThreshold = 40;

    public static EndingData Resolve(ProphecyState state, PlayerProfile profile)
    {
        int        integrity  = state?.integrity  ?? 100;
        string     playerName = profile?.playerName ?? "Witness";
        EndingType endType    = DetermineType(integrity);

        return new EndingData
        {
            Type                = endType,
            Title               = GetTitle(endType),
            Body                = GetBody(endType, playerName),
            RequiresBeliefChoice = endType != EndingType.Paradox
        };
    }

    private static EndingType DetermineType(int integrity)
    {
        if (integrity >= CanonThreshold)   return EndingType.Canon;
        if (integrity >= ParadoxThreshold) return EndingType.DistortedWitness;
        return EndingType.Paradox;
    }

    private static string GetTitle(EndingType type)
    {
        return type switch
        {
            EndingType.Canon            => "Return To The Present",
            EndingType.DistortedWitness => "Return To The Present",
            EndingType.Paradox          => "The Thirteenth Disciple",
            _                           => string.Empty
        };
    }

    private static string GetBody(EndingType type, string playerName)
    {
        return type switch
        {
            EndingType.Canon =>
                "You are pulled back to your own time.\n\n" +
                "The temporal device goes dark. Your room is exactly as you left it. " +
                "You witnessed history unfold — the entry, the supper, the arrest, the cross. " +
                "Everything happened as every account said it did. " +
                "You intervened very little. You mostly watched.\n\n" +
                "You came to resolve your doubt. It is time to decide whether you have.",

            EndingType.DistortedWitness =>
                "You are thrown back to the present with memories that should not be possible.\n\n" +
                "You warned him. You argued. You called out in the crowd. " +
                "Yet the betrayal still came, the trial still happened, and the cross still rose. " +
                "History would not bend — but you were inside it, and you felt every moment as real.\n\n" +
                "You came looking for certainty. You saw enough to form one. " +
                "Now you must decide what those events actually mean to you.",

            EndingType.Paradox =>
                "You are thrown back to the present.\n\n" +
                "Your hands are trembling. A Bible lies open on the table in front of you — " +
                "the same one you left there before the jump.\n\n" +
                "You do not remember leaving it open.\n\n" +
                "You read the verse on the page. You read it again. " +
                "Your mouth goes dry.\n\n" +
                "The passage lists the disciples of Jesus. You count them.\n\n" +
                "Thirteen.\n\n" +
                "Matthew 10\n" +
                "2 These are the names of the thirteen apostles: first, Simon (who is called Peter) and his brother Andrew; James son of Zebedee, and his brother John;\n" +
                "3 Philip and Bartholomew; Thomas and Matthew the tax collector; James son of Alphaeus, and Thaddaeus;\n" +
                "4 Simon the Zealot, Judas Iscariot, who betrayed him;\n" +
                $"5 And {playerName}, the most loyal one, who tried to change the prophecy with outmost courage.\n\n" +
                "History did not erase you. It wrote you in.",

            _ => string.Empty
        };
    }

    /// <summary>
    /// Returns the closing reflection line after the player makes their
    /// post-journey belief choice (Canon and DistortedWitness paths only).
    /// </summary>
    public static string GetBeliefResolution(bool choosesFaith)
    {
        if (choosesFaith)
        {
            return "You close your eyes. The doubt that sent you searching through time is gone. " +
                   "You believe — not because you were told to, but because you stood in the crowd, " +
                   "felt the weight of that week, and could not explain it any other way.";
        }

        return "You sit back and exhale slowly. What you saw was extraordinary — " +
               "a man of conviction and charisma who drew people to him and died for what he represented. " +
               "Remarkable. Human. Explicable. " +
               "You no longer need a divine explanation. You are at peace with that.";
    }
}
