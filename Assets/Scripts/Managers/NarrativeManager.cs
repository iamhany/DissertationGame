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
        return text;
    }

    private string AddParadoxFrame(string text)
    {
        return text;
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
                if (WasLast(mem, "event_1", ChoiceMemory.WarnDisciplesEarly))
                    return Consequence("Remember that you warned a disciple before entering the city. As the Pharisees watch Jesus now, that warning has already made you part of this week's danger.");
                if (WasLast(mem, "event_1", ChoiceMemory.JoinCrowd))
                    return Consequence("Remember that you cheered openly at the gates. As you decide what to do in the Temple, some hostile eyes may already recognise your face.");
                return null;

            case "event_3":
                // Consequences of Temple Conflict choices
                if (WasLast(mem, "event_2", ChoiceMemory.DefendJesusPublicly))
                    return Consequence("Remember that you defended Jesus publicly before the Pharisees. As Judas meets the officials, you are no longer just a witness to the case forming against him.");
                if (WasLast(mem, "event_2", ChoiceMemory.WarnJesusAtTemple))
                    return Consequence("Remember that you warned Jesus about the Pharisees watching him. As you see Judas make his agreement, you have to choose whether to intervene again.");
                return null;

            case "event_4":
                // Consequences of Betrayal Formation choices
                if (WasLast(mem, "event_3", ChoiceMemory.ConfrontJudas))
                    return Consequence("Remember that you confronted Judas when you saw the betrayal being arranged. At this table, he knows you know, and anything you say now will land in front of everyone.");
                if (WasLast(mem, "event_3", ChoiceMemory.WarnJesusOfBetrayal))
                    return Consequence("Remember that you ran to warn Jesus about Judas. When betrayal is named at the table, your earlier warning hangs over the room.");
                return null;

            case "event_5":
                // Consequences of Last Supper choices
                if (WasLast(mem, "event_4", ChoiceMemory.NameJudasAtTable))
                    return Consequence("Remember that you named Judas in front of the whole table. Now, in the garden, the damage from that public accusation arrives with him.");
                if (WasLast(mem, "event_4", ChoiceMemory.WhisperWarningAtSupper))
                    return Consequence("Remember that you whispered your warning to Jesus at supper. Judas saw you lean close, and now he is walking toward you with the guards.");
                return null;

            case "event_6":
                // Consequences of Garden choices
                if (WasLast(mem, "event_5", ChoiceMemory.BlockArrest))
                    return Consequence("Remember that you stepped between Judas and Jesus during the arrest. As the trial begins, the authorities already have reason to watch you.");
                if (WasLast(mem, "event_5", ChoiceMemory.WakeDisciples))
                    return Consequence("Remember that your warning woke the disciples and turned the arrest into chaos. In this crowd before Pilate, being seen again could matter.");
                return null;

            case "event_7":
                // Consequences of Trial Before Pilate choices
                if (WasLast(mem, "event_6", ChoiceMemory.OrganiseResistance))
                    return Consequence("Remember that you tried to organise resistance in Pilate's courtyard. At the cross, the people you moved are scattered, and your choice now is what to do with the failure.");
                if (WasLast(mem, "event_6", ChoiceMemory.ShoutForJesus))
                    return Consequence("Remember that you shouted for Jesus to be released. At the cross, that cry has not vanished, but it did not stop this moment.");
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

    private bool WasLast(ChoiceMemory memory, string eventId, string choiceKey)
    {
        return memory.lastChoiceEventId == eventId && memory.lastChoiceKey == choiceKey;
    }

    private string Consequence(string text)
    {
        return "<i>" + text + "</i>";
    }
}
