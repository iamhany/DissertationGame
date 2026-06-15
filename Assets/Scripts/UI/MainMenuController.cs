using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the Main Menu scene root.
///
/// Layout expected in the scene:
///   • New Game button  → newGameButton
///   • Continue button  → continueButton  (hidden when no save exists)
///   • Settings button  → settingsButton
///   • Name entry panel → nameEntryPanel  (hidden by default)
///       ─ TMP_InputField  → nameInputField
///       ─ Confirm button  → confirmNameButton
///   • Settings panel   → settingsPanel   (hidden by default, contains SettingsPanel component)
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Main Buttons")]
    public Button newGameButton;
    public Button continueButton;
    public Button settingsButton;

    [Header("Name Entry Panel")]
    public GameObject     nameEntryPanel;
    public TMP_InputField nameInputField;
    public Button         confirmNameButton;
    public Button         nameBackButton;

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public Button     settingsBackButton;

    void Start()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameClicked);

        if (continueButton != null)
        {
            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
            continueButton.gameObject.SetActive(hasSave);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ToggleSettings);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(ToggleSettings);

        if (confirmNameButton != null)
            confirmNameButton.onClick.AddListener(OnConfirmNameClicked);

        if (nameBackButton != null)
            nameBackButton.onClick.AddListener(OnNameBackClicked);

        // Start with sub-panels hidden
        if (nameEntryPanel != null) nameEntryPanel.SetActive(false);
        if (settingsPanel  != null) settingsPanel.SetActive(false);
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void SetMainButtonsVisible(bool visible)
    {
        if (newGameButton  != null) newGameButton.gameObject.SetActive(visible);
        if (continueButton != null && (visible || continueButton.gameObject.activeSelf))
            continueButton.gameObject.SetActive(visible && SaveManager.Instance != null && SaveManager.Instance.HasSave());
        if (settingsButton != null) settingsButton.gameObject.SetActive(visible);
    }

    private void OnNewGameClicked()
    {
        SaveManager.Instance?.ClearSave();
        if (nameEntryPanel != null) nameEntryPanel.SetActive(true);
        SetMainButtonsVisible(false);
    }

    private void OnNameBackClicked()
    {
        if (nameEntryPanel != null) nameEntryPanel.SetActive(false);
        SetMainButtonsVisible(true);
    }

    private void OnContinueClicked()
    {
        GameManager.Instance.IsLoadingSave = true;
        GameManager.Instance.StartGame();
    }

    private void OnConfirmNameClicked()
    {
        string enteredName = nameInputField != null ? nameInputField.text : string.Empty;
        GameManager.Instance.SetPlayerName(enteredName);
        GameManager.Instance.IsLoadingSave = false;
        GameManager.Instance.StartGame();
    }

    private void ToggleSettings()
    {
        if (settingsPanel == null) return;

        bool opening = !settingsPanel.activeSelf;
        settingsPanel.SetActive(opening);
        SetMainButtonsVisible(!opening);
    }
}
