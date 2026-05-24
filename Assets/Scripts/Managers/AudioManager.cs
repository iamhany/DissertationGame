using UnityEngine;

/// <summary>
/// Controls Master, Music and SFX volumes. Values are persisted via PlayerPrefs
/// so they survive between sessions. Wire an AudioSource for background music
/// in the Inspector; SFX sources read SfxVolume when they play.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MasterKey = "Vol_Master";
    private const string MusicKey  = "Vol_Music";
    private const string SfxKey    = "Vol_Sfx";

    public float MasterVolume { get; private set; }
    public float MusicVolume  { get; private set; }
    public float SfxVolume    { get; private set; }

    [Header("Background music source (optional — wire in Inspector)")]
    public AudioSource musicSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume  = PlayerPrefs.GetFloat(MusicKey,  0.8f);
        SfxVolume    = PlayerPrefs.GetFloat(SfxKey,    1f);

        ApplyAll();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        ApplyAll();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        if (musicSource != null) musicSource.volume = MusicVolume;
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
    }

    private void ApplyAll()
    {
        AudioListener.volume = MasterVolume;
        if (musicSource != null) musicSource.volume = MusicVolume;
    }
}
