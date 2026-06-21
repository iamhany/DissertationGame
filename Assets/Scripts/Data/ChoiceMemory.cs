using System;

/// <summary>
/// Tracks every bold narrative decision the player has made.
/// Referenced by NarrativeManager to inject consequence text into later events,
/// and by GameManager to decide whether to load a stealth or escape scene.
/// </summary>
[Serializable]
public class ChoiceMemory
{
    public const string JoinCrowd = "join_crowd";
    public const string WarnDisciplesEarly = "warn_disciples_early";
    public const string WarnJesusAtTemple = "warn_jesus_at_temple";
    public const string DefendJesusPublicly = "defend_jesus_publicly";
    public const string ConfrontJudas = "confront_judas";
    public const string WarnJesusOfBetrayal = "warn_jesus_of_betrayal";
    public const string WhisperWarningAtSupper = "whisper_warning_at_supper";
    public const string NameJudasAtTable = "name_judas_at_table";
    public const string WakeDisciples = "wake_disciples";
    public const string BlockArrest = "block_arrest";
    public const string ShoutForJesus = "shout_for_jesus";
    public const string OrganiseResistance = "organise_resistance";

    public string lastChoiceEventId;
    public string lastChoiceKey;
    public bool lastChoiceWasSecondOption;
    public bool lastChoiceWasBoldestOption;
    public bool hasChosenSecondOption;
    public int secondOptionChoiceCountSinceBoldest;
    public int boldestOptionChoiceCount;
    public bool escapeScenePlayed;

    // ── event_1 choices ──────────────────────────────────────────────────────
    public bool joinedCrowd;           // cheered during Palm Sunday entry
    public bool warnedDisciplesEarly;  // spoke to disciples at the entry

    // ── event_2 choices ──────────────────────────────────────────────────────
    public bool warnedJesusAtTemple;   // told Jesus the Pharisees were watching
    public bool defendedJesusPublicly; // confronted Pharisees face-to-face

    // ── event_3 choices ──────────────────────────────────────────────────────
    public bool confrontedJudas;       // directly challenged Judas
    public bool warnedJesusOfBetrayal; // ran to warn Jesus about Judas

    // ── event_4 choices ──────────────────────────────────────────────────────
    public bool whisperWarningAtSupper;  // quiet warning to Jesus at Last Supper
    public bool namedJudasAtTable;       // outed Judas before all disciples

    // ── event_5 choices ──────────────────────────────────────────────────────
    public bool wokeTheDisciples;      // shouted warning in the garden
    public bool blockedTheArrest;      // physically stepped between Judas & Jesus

    // ── event_6 choices ──────────────────────────────────────────────────────
    public bool shoutedForJesus;       // cried out in the trial crowd
    public bool organisedResistance;   // worked the crowd to resist

    // ── Derived helpers ───────────────────────────────────────────────────────

    /// <summary>Player made at least one public stand that would mark them as a
    /// defender of Jesus — historically a capital offence.</summary>
    public bool MadePublicDefence =>
        defendedJesusPublicly || blockedTheArrest || shoutedForJesus || organisedResistance;

    public bool LastChoiceWasPublicDefence =>
        lastChoiceKey == DefendJesusPublicly ||
        lastChoiceKey == BlockArrest ||
        lastChoiceKey == ShoutForJesus ||
        lastChoiceKey == OrganiseResistance;

    public bool ShouldEscapeAfterLastChoice =>
        (lastChoiceWasSecondOption && secondOptionChoiceCountSinceBoldest >= 2) ||
        (lastChoiceWasBoldestOption &&
            (LastChoiceWasPublicDefence || hasChosenSecondOption || boldestOptionChoiceCount > 1));

    /// <summary>Player actively tried to interfere with Judas specifically.</summary>
    public bool TriedToStopJudas => confrontedJudas || warnedJesusOfBetrayal || blockedTheArrest;

    /// <summary>Total number of interventions made.</summary>
    public int InterventionCount
    {
        get
        {
            int n = 0;
            if (joinedCrowd)             n++;
            if (warnedDisciplesEarly)    n++;
            if (warnedJesusAtTemple)     n++;
            if (defendedJesusPublicly)   n++;
            if (confrontedJudas)         n++;
            if (warnedJesusOfBetrayal)   n++;
            if (whisperWarningAtSupper)  n++;
            if (namedJudasAtTable)       n++;
            if (wokeTheDisciples)        n++;
            if (blockedTheArrest)        n++;
            if (shoutedForJesus)         n++;
            if (organisedResistance)     n++;
            return n;
        }
    }

    public void Reset()
    {
        joinedCrowd = warnedDisciplesEarly = warnedJesusAtTemple =
        defendedJesusPublicly = confrontedJudas = warnedJesusOfBetrayal =
        whisperWarningAtSupper = namedJudasAtTable = wokeTheDisciples =
        blockedTheArrest = shoutedForJesus = organisedResistance = false;
        lastChoiceEventId = null;
        lastChoiceKey = null;
        lastChoiceWasSecondOption = false;
        lastChoiceWasBoldestOption = false;
        hasChosenSecondOption = false;
        secondOptionChoiceCountSinceBoldest = 0;
        boldestOptionChoiceCount = 0;
        escapeScenePlayed = false;
    }
}
