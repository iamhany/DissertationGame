using System;

/// <summary>
/// Serializable snapshot of all game progress. Written to disk by SaveManager.
/// </summary>
[Serializable]
public class GameSaveData
{
    public bool   hasSave;
    public string playerName             = "Witness";
    public int    currentEventIndex;

    // ProphecyState
    public int    prophecyIntegrity      = 100;
    public bool   playerAbsorbed;

    // InterpretationState
    public int    faithVsSkepticism;
    public int    literalVsSymbolic;
    public int    trustInAuthority;

    // CrowdState
    public int    proJesus;
    public int    neutral                = 50;
    public int    antiJesus;
}
