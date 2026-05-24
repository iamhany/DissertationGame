using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Attach to the Settings panel GameObject.
/// Wires three Sliders to AudioManager so changes are applied and saved in real time.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Percentage labels (optional)")]
    public TMP_Text masterLabel;
    public TMP_Text musicLabel;
    public TMP_Text sfxLabel;

    // Re-sync slider positions whenever the panel becomes visible
    void OnEnable()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        SetupSlider(masterSlider, am.MasterVolume,
            v => AudioManager.Instance?.SetMasterVolume(v), masterLabel);
        SetupSlider(musicSlider, am.MusicVolume,
            v => AudioManager.Instance?.SetMusicVolume(v),  musicLabel);
        SetupSlider(sfxSlider, am.SfxVolume,
            v => AudioManager.Instance?.SetSfxVolume(v),    sfxLabel);
    }

    private void SetupSlider(Slider slider, float initial,
                              UnityAction<float> onChange, TMP_Text label)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = initial;

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(onChange);
        slider.onValueChanged.AddListener(v => UpdateLabel(label, v));

        UpdateLabel(label, initial);
    }

    private static void UpdateLabel(TMP_Text label, float value)
    {
        if (label != null)
            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
