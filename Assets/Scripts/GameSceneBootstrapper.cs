using UnityEngine;

/// <summary>
/// Place this on a single GameObject in the Game scene.
/// Registers all managers with GameManager, restores save data if continuing,
/// then kicks off the event sequence at the correct position.
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

        if (GameManager.Instance.IsLoadingSave)
        {
            GameSaveData saveData = SaveManager.Instance?.Load();
            if (saveData != null && saveData.hasSave)
            {
                SaveManager.Instance.RestoreState(saveData);
                startIndex = saveData.currentEventIndex;
            }
            GameManager.Instance.IsLoadingSave = false;
        }

        EventManager.Instance.BeginSequence(startIndex);
    }
}
