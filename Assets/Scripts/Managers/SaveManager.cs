using System.IO;
using UnityEngine;

/// <summary>
/// Persists game progress to a JSON file in Application.persistentDataPath.
/// Triggered by EventManager after each choice and cleared when the game ends.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const  string SaveFileName = "temporalwitness.sav";
    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool HasSave()
    {
        if (!File.Exists(SavePath)) return false;
        GameSaveData data = Load();
        return data != null && data.hasSave;
    }

    public void Save()
    {
        if (GameManager.Instance     == null ||
            StateManager.Instance    == null ||
            ProphecyManager.Instance == null ||
            EventManager.Instance    == null) return;

        var data = new GameSaveData
        {
            hasSave           = true,
            playerName        = GameManager.Instance.PlayerProfile.playerName,
            currentEventIndex = EventManager.Instance.CurrentEventIndex,

            prophecyIntegrity = ProphecyManager.Instance.State.integrity,
            playerAbsorbed    = ProphecyManager.Instance.State.playerAbsorbedIntoHistory,

            faithVsSkepticism = StateManager.Instance.Interpretation.faithVsSkepticism,
            literalVsSymbolic = StateManager.Instance.Interpretation.literalVsSymbolic,
            trustInAuthority  = StateManager.Instance.Interpretation.trustInAuthority,

            proJesus  = StateManager.Instance.Crowd.proJesus,
            neutral   = StateManager.Instance.Crowd.neutral,
            antiJesus = StateManager.Instance.Crowd.antiJesus
        };

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
        }
    }

    public GameSaveData Load()
    {
        if (!File.Exists(SavePath)) return null;
        try
        {
            return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Load failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Applies loaded save data to all live manager instances.</summary>
    public void RestoreState(GameSaveData data)
    {
        if (data == null) return;

        GameManager.Instance.SetPlayerName(data.playerName);

        ProphecyManager.Instance.State.integrity                = data.prophecyIntegrity;
        ProphecyManager.Instance.State.playerAbsorbedIntoHistory = data.playerAbsorbed;

        StateManager.Instance.Interpretation.faithVsSkepticism = data.faithVsSkepticism;
        StateManager.Instance.Interpretation.literalVsSymbolic = data.literalVsSymbolic;
        StateManager.Instance.Interpretation.trustInAuthority  = data.trustInAuthority;

        StateManager.Instance.Crowd.proJesus  = data.proJesus;
        StateManager.Instance.Crowd.neutral   = data.neutral;
        StateManager.Instance.Crowd.antiJesus = data.antiJesus;
    }

    public void ClearSave()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveManager] Clear failed: {ex.Message}");
        }
    }
}
