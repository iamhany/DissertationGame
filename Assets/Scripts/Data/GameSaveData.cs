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

    // ChoiceMemory
    public bool joinedCrowd;
    public bool warnedDisciplesEarly;
    public bool warnedJesusAtTemple;
    public bool defendedJesusPublicly;
    public bool confrontedJudas;
    public bool warnedJesusOfBetrayal;
    public bool whisperWarningAtSupper;
    public bool namedJudasAtTable;
    public bool wokeTheDisciples;
    public bool blockedTheArrest;
    public bool shoutedForJesus;
    public bool organisedResistance;
    public string lastChoiceEventId;
    public string lastChoiceKey;
    public bool lastChoiceWasSecondOption;
    public bool lastChoiceWasBoldestOption;
    public bool hasChosenSecondOption;
    public int secondOptionChoiceCountSinceBoldest;
    public int boldestOptionChoiceCount;
    public bool escapeScenePlayed;
}
