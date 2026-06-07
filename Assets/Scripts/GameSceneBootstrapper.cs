using UnityEngine;

/// <summary>
/// Place this on a single GameObject in the Game scene.
/// Registers all managers with GameManager, restores save data if continuing,
/// then kicks off the event sequence at the correct position.
/// Also handles resuming after a stealth or escape gameplay scene.
/// </summary>
public class GameSceneBootstrapper : MonoBehaviour
{
    void Start()
    {
        GameManager.Instance.RegisterManagers(
            StateManager.Instance,
            ProphecyManager.Instance,
            EventManager.Instance,
            NarrativeManager.Instance,
            UIManager.Instance
        );

        int startIndex = 0;

        // Returning from a gameplay scene (stealth / escape)
        if (EventManager.Instance.ResumeIndex > 0)
        {
            startIndex = EventManager.Instance.ResumeIndex;
            EventManager.Instance.ResumeIndex = 0;
        }
        else if (GameManager.Instance.IsLoadingSave)
        {
            GameSaveData saveData = SaveManager.Instance?.Load();
            if (saveData != null && saveData.hasSave)
            {
                SaveManager.Instance.RestoreState(saveData);
                startIndex = saveData.currentEventIndex;
            }
            GameManager.Instance.IsLoadingSave = false;
        }

        // Delay one frame so all Start() calls (including UIManager's subscription)
        // complete before BeginSequence fires its synchronous OnEventLoaded event.
        StartCoroutine(DelayedBegin(startIndex));
    }

    private System.Collections.IEnumerator DelayedBegin(int index)
    {
        yield return null;
        EventManager.Instance.BeginSequence(index);
    }
}
