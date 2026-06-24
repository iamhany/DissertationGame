using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls Master, Music and SFX volumes, and owns the game's music/SFX sources.
/// Values are persisted via PlayerPrefs so they survive between sessions.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MasterKey = "Vol_Master";
    private const string MusicKey  = "Vol_Music";
    private const string SfxKey    = "Vol_Sfx";
    private const string SoundResourcePath = "Sounds/";

    public float MasterVolume { get; private set; }
    public float MusicVolume  { get; private set; }
    public float SfxVolume    { get; private set; }

    [Header("Audio sources (optional)")]
    public AudioSource musicSource;
    public AudioSource ambientSource;
    public AudioSource movementSource;
    public AudioSource sfxSource;
    public AudioSource narrativeEffectSource;
    public AudioListener fallbackListener;

    [Header("Music")]
    public AudioClip soundtrackClip;
    public AudioClip ambientNoiseClip;
    public AudioClip quietNightClip;

    [Header("SFX")]
    public AudioClip clickClip;
    public AudioClip runningClip;
    public AudioClip nitroClip;
    public AudioClip heyClip;
    public AudioClip teleportClip;

    private static readonly string[] StealthSceneNames = { "ExplorationScene", "StealthScene" };
    private readonly System.Collections.Generic.HashSet<string> _playedTeleportKeys =
        new System.Collections.Generic.HashSet<string>();
    private Coroutine _teleportFade;
    private Coroutine _musicFade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static AudioManager EnsureExists()
    {
        if (Instance != null) return Instance;
        return new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume  = PlayerPrefs.GetFloat(MusicKey,  0.8f);
        SfxVolume    = PlayerPrefs.GetFloat(SfxKey,    1f);
        LoadMissingClips();
        Debug.Log(
            $"AudioManager ready. Master={MasterVolume:0.00}, Music={MusicVolume:0.00}, SFX={SfxVolume:0.00}, " +
            $"soundtrack={(soundtrackClip != null ? soundtrackClip.name : "missing")}.");

        musicSource    = EnsureSource(musicSource, "MusicSource", true);
        ambientSource  = EnsureSource(ambientSource, "AmbientSource", true);
        movementSource = EnsureSource(movementSource, "MovementSource", true);
        sfxSource      = EnsureSource(sfxSource, "SfxSource", false);
        narrativeEffectSource = EnsureSource(narrativeEffectSource, "NarrativeEffectSource", true);
        EnsureListener();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyAll();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
            ApplySceneAudio(activeScene);
    }

    void Start()
    {
        ApplySceneAudio(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
        ApplyMusicVolumes();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        ApplySfxVolumes();
    }

    public void PlayChoiceClick()
    {
        PlaySfx(clickClip);
    }

    public void PlayGuardHey()
    {
        PlaySfx(heyClip);
    }

    public void UpdateStealthMovementAudio(bool isMoving, bool isUsingNitro)
    {
        if (!isMoving)
        {
            StopMovementLoop();
            return;
        }

        AudioClip targetClip = isUsingNitro ? nitroClip : runningClip;
        if (targetClip == null) return;

        if (movementSource.clip == targetClip && movementSource.isPlaying) return;

        movementSource.clip = targetClip;
        movementSource.loop = true;
        movementSource.volume = SfxVolume;
        movementSource.Play();
    }

    public void StopMovementLoop()
    {
        if (movementSource == null || !movementSource.isPlaying) return;
        movementSource.Stop();
    }

    private void ApplyAll()
    {
        AudioListener.volume = MasterVolume;
        ApplyMusicVolumes();
        ApplySfxVolumes();
    }

    private void ApplyMusicVolumes()
    {
        if (musicSource   != null) musicSource.volume = MusicVolume;
        if (ambientSource != null) ambientSource.volume = MusicVolume;
    }

    private void ApplySfxVolumes()
    {
        if (movementSource != null) movementSource.volume = SfxVolume;
        if (sfxSource      != null) sfxSource.volume = SfxVolume;
        if (narrativeEffectSource != null) narrativeEffectSource.volume = SfxVolume;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureListener();
        ApplySceneAudio(scene);
    }

    private void ApplySceneAudio(Scene scene)
    {
        if (!scene.IsValid())
        {
            PlaySoundtrack();
            return;
        }

        if (IsStealthSceneLoaded(scene))
            PlayStealthAmbient();
        else
            PlaySoundtrack();
    }

    private bool IsStealthSceneLoaded(Scene scene)
    {
        if (IsStealthSceneName(scene.name)) return true;

        foreach (string sceneName in StealthSceneNames)
        {
            Scene stealthScene = SceneManager.GetSceneByName(sceneName);
            if (stealthScene.IsValid() && stealthScene.isLoaded)
                return true;
        }

        return false;
    }

    private static bool IsStealthSceneName(string sceneName)
    {
        foreach (string stealthSceneName in StealthSceneNames)
        {
            if (sceneName == stealthSceneName)
                return true;
        }

        return false;
    }

    private void PlaySoundtrack()
    {
        StopMovementLoop();
        StopSource(ambientSource);
        PlayLoop(musicSource, soundtrackClip, MusicVolume);
    }

    public void PlayQuietNightMusic()
    {
        CrossfadeMusic(quietNightClip, 2.5f);
    }

    private void CrossfadeMusic(AudioClip nextClip, float duration)
    {
        if (musicSource == null || nextClip == null) return;

        if (_musicFade != null)
            StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(CrossfadeMusicRoutine(nextClip, duration));
    }

    private System.Collections.IEnumerator CrossfadeMusicRoutine(AudioClip nextClip, float duration)
    {
        StopMovementLoop();
        StopSource(ambientSource);

        duration = Mathf.Max(0.01f, duration);
        float halfDuration = duration * 0.5f;
        float startVolume = musicSource.isPlaying ? musicSource.volume : 0f;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = nextClip;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.Play();

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, MusicVolume, elapsed / halfDuration);
            yield return null;
        }

        musicSource.volume = MusicVolume;
        _musicFade = null;
    }

    private void PlayStealthAmbient()
    {
        StopMovementLoop();
        StopSource(musicSource);
        PlayLoop(ambientSource, ambientNoiseClip, MusicVolume);
    }

    private void PlayLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null)
        {
            Debug.LogWarning("AudioManager could not play loop because the AudioSource is missing.");
            return;
        }
        if (clip == null)
        {
            Debug.LogWarning("AudioManager could not play loop because the AudioClip is missing.");
            return;
        }

        if (source.clip == clip && source.isPlaying)
        {
            source.volume = volume;
            return;
        }

        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.Play();
        Debug.Log($"AudioManager playing '{clip.name}' at volume {volume:0.00}.");
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, SfxVolume);
    }

    public void PlayTeleportOnce(string key = "default")
    {
        key = string.IsNullOrEmpty(key) ? "default" : key;
        if (_playedTeleportKeys.Contains(key) || narrativeEffectSource == null || teleportClip == null) return;

        if (_teleportFade != null)
        {
            StopCoroutine(_teleportFade);
            _teleportFade = null;
        }

        narrativeEffectSource.clip = teleportClip;
        narrativeEffectSource.loop = false;
        narrativeEffectSource.volume = SfxVolume;
        narrativeEffectSource.Play();
        _playedTeleportKeys.Add(key);
    }

    public void FadeOutTeleport(float duration = 0.6f)
    {
        if (narrativeEffectSource == null || !narrativeEffectSource.isPlaying) return;

        if (_teleportFade != null)
            StopCoroutine(_teleportFade);
        _teleportFade = StartCoroutine(FadeOutTeleportRoutine(duration));
    }

    private System.Collections.IEnumerator FadeOutTeleportRoutine(float duration)
    {
        float startVolume = narrativeEffectSource.volume;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            narrativeEffectSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        narrativeEffectSource.Stop();
        narrativeEffectSource.clip = null;
        narrativeEffectSource.volume = SfxVolume;
        _teleportFade = null;
    }

    public void ResetTeleportPlaybackHistory()
    {
        _playedTeleportKeys.Clear();
    }

    private void LoadMissingClips()
    {
        soundtrackClip   = LoadClipIfMissing(soundtrackClip,   "soundtrack");
        ambientNoiseClip = LoadClipIfMissing(ambientNoiseClip, "ambientnoise");
        quietNightClip   = LoadClipIfMissing(quietNightClip,   "quietnight");
        clickClip        = LoadClipIfMissing(clickClip,        "click");
        runningClip      = LoadClipIfMissing(runningClip,      "running");
        nitroClip        = LoadClipIfMissing(nitroClip,        "nitro");
        heyClip          = LoadClipIfMissing(heyClip,          "hey");
        teleportClip     = LoadClipIfMissing(teleportClip,     "teleport");
    }

    private static AudioClip LoadClipIfMissing(AudioClip clip, string resourceName)
    {
        if (clip != null) return clip;

        AudioClip loaded = Resources.Load<AudioClip>(SoundResourcePath + resourceName);
        if (loaded == null)
            Debug.LogWarning($"AudioManager could not load Resources/{SoundResourcePath}{resourceName}.");
        return loaded;
    }

    private AudioSource EnsureSource(AudioSource source, string name, bool loop)
    {
        if (source != null)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var created = child.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.loop = loop;
        created.spatialBlend = 0f;
        return created;
    }

    private void EnsureListener()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
            {
                if (fallbackListener != null)
                    fallbackListener.enabled = false;
                return;
            }
        }

        if (fallbackListener == null)
            fallbackListener = gameObject.AddComponent<AudioListener>();
        fallbackListener.enabled = true;
    }

    private static void StopSource(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }
}
