using UnityEngine;

/// <summary>
/// Selects which flavour of narrative text to surface based on the current
/// prophecy integrity, injects consequence text reflecting earlier player choices,
/// then substitutes any dynamic tokens (e.g. {playerName}).
/// </summary>
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Returns the display text for the current event, rewritten according to
    /// how far the prophecy has been stressed. If a SnapbackResult provides an
    /// override, that takes precedence. Choice-memory consequences are appended.
    /// </summary>
    public string GetDisplayText(NarrativeEvent evt, SnapbackResult snapback)
    {
        string baseText;

        if (snapback.WasTriggered && !string.IsNullOrEmpty(snapback.OverrideText))
        {
            baseText = snapback.OverrideText;
        }
        else
        {
            int integrity = ProphecyManager.Instance.Integrity;

            if (integrity >= 70)
                baseText = evt.text;
            else if (integrity >= 40)
                baseText = AddDistortedFrame(evt.text);
            else
                baseText = AddParadoxFrame(evt.text);
        }

        // Append consequence paragraph reflecting prior choices
        string consequence = GetConsequenceText(evt.id);
        if (!string.IsNullOrEmpty(consequence))
            baseText = baseText + "\n\n" + consequence;

        return ResolveTokens(baseText);
    }

    // ── Integrity framing ─────────────────────────────────────────────────────

    private string AddDistortedFrame(string text)
    {
        return text + "\n\n[The record shifts. Your presence is felt but not yet named.]";
    }

    private string AddParadoxFrame(string text)
    {
        return text + "\n\n[History strains against your intervention. Something in the account must give.]";
    }

    // ── Choice-memory consequences ────────────────────────────────────────────

    private string GetConsequenceText(string eventId)
    {
        var mem = StateManager.Instance?.Memory;
        if (mem == null) return null;

        switch (eventId)
        {
            case "event_2":
                // Consequences of what the player did at the Entry
                if (mem.warnedDisciplesEarly)
                    return "One of the disciples you spoke to earlier glances at you with recognition across the Temple courtyard. He nods — barely. He has not forgotten what you told him.";
                if (mem.joinedCrowd)
                    return "A Pharisee near the colonnade is staring at you. You were too visible at the gates. He is trying to place your face.";
                return null;

            case "event_3":
                // Consequences of Temple Conflict choices
                if (mem.defendedJesusPublicly)
                    return "The scribes who were documenting the incident have scattered — but one of them saw your face when you confronted them. You are now part of the record.\n\nThis is not a time in history when speaking against the authorities is a protected right. It is a death sentence waiting to be signed.";
                if (mem.warnedJesusAtTemple)
                    return "Jesus paused when you approached him. He looked at you with unsettling calm, as though your warning was not a surprise to him — but that you, specifically, had given it, was.";
                return null;

            case "event_4":
                // Consequences of Betrayal Formation choices
                if (mem.confrontedJudas)
                    return "Judas is at the table. His eyes find you the moment you enter the room. He knows you saw him. His jaw tightens. He does not speak, but his hand moves toward the edge of the table — and you notice he is sitting closest to the door.\n\nHe will report you to the Temple officials. It is only a matter of time.";
                if (mem.warnedJesusOfBetrayal)
                    return "When Jesus says 'one of you will betray me,' there is a half-second — just one — where his eyes are on you before he sweeps the room. He already knew. You confirmed it. You are now woven into this moment in a way no historian will be able to explain.";
                return null;

            case "event_5":
                // Consequences of Last Supper choices
                if (mem.namedJudasAtTable)
                    return "The disciples are rattled. Some believe you. Some think you're an infiltrator. Either way, the room erupted when you named Judas — and three people saw your face clearly in the lamplight before you were pushed aside.\n\nThose three faces are in the crowd outside the garden right now. The Temple guards have been told to watch for a stranger.";
                if (mem.whisperWarningAtSupper)
                    return "Jesus's expression did not change when you whispered to him. He thanked you in Aramaic — quietly, so only you could hear. Then he continued breaking bread as if nothing had changed.\n\nBut Judas saw you lean in. And Judas is now at the front of the guard column approaching through the trees.";
                if (mem.confrontedJudas)
                    return "Judas walks faster than the others. He has something to prove tonight. The confrontation with you earlier stoked something cold in him — a need to complete this, to make it irreversible.\n\nHe has also told the guards to look for a stranger who was interfering.";
                return null;

            case "event_6":
                // Consequences of Garden choices
                if (mem.blockedTheArrest)
                    return "The guards remember you. One of them — the senior officer — points you out to another before the trial begins. You have drawn the attention of the high priest's household. In Jerusalem in 30 AD, that is not the kind of attention you survive by standing still.\n\nThe walls of the city are being watched. Someone has your description.";
                if (mem.wokeTheDisciples)
                    return "Two of the disciples are still in the city. Peter cut off a guard's ear in the chaos — and fled. The Temple officials are now looking for anyone connected to the disturbance. You were seen near the garden.";
                return null;

            case "event_7":
                // Consequences cascade into the final scene
                if (mem.MadePublicDefence)
                    return "Somewhere behind you in the crowd, a Temple guard is moving. He was at the trial. He was at the garden. He has seen your face too many times now.\n\nYou did not come here to die. But you chose, again and again, to be visible.";
                if (mem.TriedToStopJudas)
                    return "You tried to stop the betrayal. You stood in its way. And still — the cross is here. History did not need Judas to be willing. It only needed the outcome to occur. Another hand would have done it. The prophecy was not about one man's weakness. It was about a world that was always going to do this.";
                return null;

            default:
                return null;
        }
    }

    // ── Token substitution ────────────────────────────────────────────────────

    private string ResolveTokens(string text)
    {
        string playerName = GameManager.Instance?.PlayerProfile?.playerName ?? "Witness";
        return text.Replace("{playerName}", playerName);
    }
}
