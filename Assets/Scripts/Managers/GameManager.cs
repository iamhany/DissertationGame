using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerProfile PlayerProfile { get; private set; } = new PlayerProfile();

    // Manager references — assigned in Awake after singletons self-register
    public StateManager    StateManager    { get; private set; }
    public ProphecyManager ProphecyManager { get; private set; }
    public EventManager    EventManager    { get; private set; }
    public NarrativeManager NarrativeManager { get; private set; }
    public UIManager       UIManager       { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Called by MainMenuController once the player has entered their name
    public void SetPlayerName(string name)
    {
        PlayerProfile.playerName = string.IsNullOrWhiteSpace(name) ? "Witness" : name.Trim();
    }

    // Called once per scene load to re-bind manager refs (handles additive loads too)
    public void RegisterManagers(StateManager sm, ProphecyManager pm, EventManager em,
                                  NarrativeManager nm, UIManager ui)
    {
        StateManager     = sm;
        ProphecyManager  = pm;
        EventManager     = em;
        NarrativeManager = nm;
        UIManager        = ui;
    }

    /// <summary>When true the GameSceneBootstrapper restores state from the save file.</summary>
    public bool IsLoadingSave { get; set; }

    public void StartGame()
    {
        SceneManager.LoadScene("Game");
    }

    public void TriggerEnding()
    {
        SceneManager.LoadScene("Ending");
    }

    /// <summary>
    /// Clears the save, resets all live state, and returns to the Main Menu.
    /// Called by the exit menu's "Take me back" button.
    /// </summary>
    public void RestartGame()
    {
        SaveManager.Instance?.ClearSave();
        IsLoadingSave = false;
        StateManager.Instance?.ResetState();
        ProphecyManager.Instance?.ResetState();
        SceneManager.LoadScene("MainMenu");
    }
}
