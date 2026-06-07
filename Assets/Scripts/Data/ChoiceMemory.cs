using System;

/// <summary>
/// Tracks every bold narrative decision the player has made.
/// Referenced by NarrativeManager to inject consequence text into later events,
/// and by GameManager to decide whether to load a stealth or escape scene.
/// </summary>
[Serializable]
public class ChoiceMemory
{
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
        defendedJesusPublicly || namedJudasAtTable || blockedTheArrest || organisedResistance;

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
    }
}
