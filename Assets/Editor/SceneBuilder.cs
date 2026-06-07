using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Editor utility: DissertationGame ▶ Build All Scenes
///
/// Creates the ChoiceButton prefab, then builds MainMenu, Game, and Ending scenes
/// with full GameObjects hierarchies, all Inspector fields wired, and registers them
/// in Build Settings (MainMenu = 0, Game = 1, Ending = 2).
/// </summary>
public static class SceneBuilder
{
    private const string PrefabDir    = "Assets/Prefabs";
    private const string PrefabPath   = "Assets/Prefabs/ChoiceButton.prefab";
    private const string SceneDir     = "Assets/Scenes";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string GamePath     = "Assets/Scenes/Game.unity";
    private const string EndingPath   = "Assets/Scenes/Ending.unity";
    private const string StealthPath  = "Assets/Scenes/StealthScene.unity";
    private const string EscapePath   = "Assets/Scenes/EscapeScene.unity";

    [MenuItem("DissertationGame/Build All Scenes")]
    public static void BuildAllScenes()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[SceneBuilder] Stop Play mode before building scenes.");
            return;
        }

        EnsureDir(PrefabDir);
        EnsureDir(SceneDir);

        var prefab = CreateChoiceButtonPrefab();

        BuildMainMenuScene();
        BuildGameScene(prefab);
        BuildEndingScene();
        BuildStealthScene();
        BuildEscapeScene();
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SceneBuilder] Done. Open MainMenu.unity and press Play.");
    }

    // ─── ChoiceButton Prefab ──────────────────────────────────────────────────

    private static GameObject CreateChoiceButtonPrefab()
    {
        var root = new GameObject("ChoiceButton");
        root.AddComponent<RectTransform>();
        var img = root.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        root.AddComponent<Button>();
        var cb = root.AddComponent<ChoiceButton>();

        var le = root.AddComponent<LayoutElement>();
        le.minHeight = 55f;
        le.flexibleWidth = 1f;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(root.transform, false);
        var rt = labelGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = new Vector2(12, 4);
        rt.offsetMax = new Vector2(-12, -4);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Choice";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        cb.label = tmp;

        var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[SceneBuilder] ChoiceButton prefab → {PrefabPath}");
        return prefabAsset;
    }

    // ─── MainMenu Scene ───────────────────────────────────────────────────────

    private static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        // ── DontDestroyOnLoad singletons ──────────────────────────────────────
        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("SaveManager").AddComponent<SaveManager>();
        new GameObject("AudioManager").AddComponent<AudioManager>();

        // ── Canvas ────────────────────────────────────────────────────────────
        var canvas = CreateCanvas("Canvas");

        // Background
        var bg = CreatePanel("Background", canvas.transform, Color.black);
        SetFill(bg);

        // Title
        var titleGO = CreateTMP("TitleText", canvas.transform,
            "The Time Witness", 56, TextAlignmentOptions.Center, Color.white);
        SetAnchors(titleGO.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.96f));

        // Subtitle
        var subtitleGO = CreateTMP("SubtitleText", canvas.transform,
            "A Narrative Journey Through Biblical History",
            22, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));
        SetAnchors(subtitleGO.GetComponent<RectTransform>(),
            new Vector2(0.15f, 0.66f), new Vector2(0.85f, 0.76f));

        // ── Button column ─────────────────────────────────────────────────────
        var btnColRT = EmptyRect("ButtonColumn", canvas.transform);
        SetAnchors(btnColRT, new Vector2(0.35f, 0.30f), new Vector2(0.65f, 0.64f));
        var vlg = btnColRT.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        var newGameBtn  = CreateMenuButton("NewGameButton",  btnColRT, "New Game");
        var continueBtn = CreateMenuButton("ContinueButton", btnColRT, "Continue");
        var settingsBtn = CreateMenuButton("SettingsButton", btnColRT, "Settings");

        // ── Name Entry Panel (hidden) ─────────────────────────────────────────
        var namePanelGO = CreatePanel("NameEntryPanel", canvas.transform,
            new Color(0.05f, 0.05f, 0.05f, 0.96f));
        SetAnchors(namePanelGO.GetComponent<RectTransform>(),
            new Vector2(0.25f, 0.34f), new Vector2(0.75f, 0.66f));

        var namePanelVLG = namePanelGO.AddComponent<VerticalLayoutGroup>();
        namePanelVLG.spacing = 16f;
        namePanelVLG.padding = new RectOffset(20, 20, 16, 16);
        namePanelVLG.childControlWidth = true;
        namePanelVLG.childControlHeight = false;
        namePanelVLG.childForceExpandWidth = true;
        namePanelVLG.childForceExpandHeight = false;
        namePanelVLG.childAlignment = TextAnchor.MiddleCenter;

        CreateTMP("PromptLabel", namePanelGO.transform,
            "Enter your name, or leave blank to be 'Witness'",
            18, TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 0.9f));

        var inputField = CreateTMPInputField("NameInputField", namePanelGO.transform);

        var confirmBtn = CreateMenuButton("ConfirmNameButton", namePanelGO.transform, "Confirm");
        ((Button)confirmBtn).GetComponent<LayoutElement>().minHeight = 48f;
        namePanelGO.SetActive(false);

        // ── Settings Panel (hidden) ───────────────────────────────────────────
        var settingsPanelGO = CreatePanel("SettingsPanel", canvas.transform,
            new Color(0.05f, 0.05f, 0.05f, 0.96f));
        SetAnchors(settingsPanelGO.GetComponent<RectTransform>(),
            new Vector2(0.20f, 0.18f), new Vector2(0.80f, 0.82f));

        var spVLG = settingsPanelGO.AddComponent<VerticalLayoutGroup>();
        spVLG.spacing = 20f;
        spVLG.padding = new RectOffset(24, 24, 24, 24);
        spVLG.childControlWidth = true;
        spVLG.childControlHeight = false;
        spVLG.childForceExpandWidth = true;
        spVLG.childForceExpandHeight = false;
        spVLG.childAlignment = TextAnchor.MiddleCenter;

        CreateTMP("SettingsTitle", settingsPanelGO.transform,
            "Settings", 28, TextAlignmentOptions.Center, Color.white);

        var (masterSlider, masterLabel) = CreateSliderRow("Master", settingsPanelGO.transform, 1f);
        var (musicSlider,  musicLabel)  = CreateSliderRow("Music",  settingsPanelGO.transform, 0.8f);
        var (sfxSlider,    sfxLabel)    = CreateSliderRow("SFX",    settingsPanelGO.transform, 1f);

        var settingsComp = settingsPanelGO.AddComponent<SettingsPanel>();
        settingsComp.masterSlider = masterSlider;
        settingsComp.musicSlider  = musicSlider;
        settingsComp.sfxSlider    = sfxSlider;
        settingsComp.masterLabel  = masterLabel;
        settingsComp.musicLabel   = musicLabel;
        settingsComp.sfxLabel     = sfxLabel;

        var settingsBackBtn = CreateMenuButton("SettingsBackButton", settingsPanelGO.transform, "← Back");
        ((Button)settingsBackBtn).GetComponent<LayoutElement>().minHeight = 44f;

        settingsPanelGO.SetActive(false);

        // ── EventSystem ───────────────────────────────────────────────────────
        CreateEventSystem();

        // ── MainMenuController ────────────────────────────────────────────────
        var mmcGO = new GameObject("MainMenuController");
        var mmc = mmcGO.AddComponent<MainMenuController>();
        mmc.newGameButton     = newGameBtn;
        mmc.continueButton    = continueBtn;
        mmc.settingsButton    = settingsBtn;
        mmc.nameEntryPanel    = namePanelGO;
        mmc.nameInputField    = inputField;
        mmc.confirmNameButton = confirmBtn;
        mmc.settingsPanel     = settingsPanelGO;
        mmc.settingsBackButton = (Button)settingsBackBtn;

        EditorSceneManager.SaveScene(scene, MainMenuPath);
        Debug.Log($"[SceneBuilder] MainMenu → {MainMenuPath}");
    }

    // ─── Game Scene ───────────────────────────────────────────────────────────

    private static void BuildGameScene(GameObject choiceButtonPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        // Bootstrapper
        new GameObject("Bootstrapper").AddComponent<GameSceneBootstrapper>();

        // Scene-specific managers
        new GameObject("StateManager").AddComponent<StateManager>();
        new GameObject("ProphecyManager").AddComponent<ProphecyManager>();
        new GameObject("EventManager").AddComponent<EventManager>();
        new GameObject("NarrativeManager").AddComponent<NarrativeManager>();
        var uiManagerGO = new GameObject("UIManager");
        var uiManager   = uiManagerGO.AddComponent<UIManager>();

        // ── Canvas ────────────────────────────────────────────────────────────
        var canvas = CreateCanvas("Canvas");

        // Background
        var bg = CreatePanel("Background", canvas.transform, Color.black);
        SetFill(bg);

        // ── FadePanel (full-screen black overlay) ─────────────────────────────
        var fadePanelGO = CreatePanel("FadePanel", canvas.transform, Color.black);
        SetFill(fadePanelGO);
        // FadeController requires CanvasGroup via [RequireComponent] — AddComponent order matters
        fadePanelGO.AddComponent<CanvasGroup>();
        var fadeCtrl = fadePanelGO.AddComponent<FadeController>();

        // ── DialoguePanel ─────────────────────────────────────────────────────
        var dialoguePanelGO = CreatePanel("DialoguePanel", canvas.transform,
            new Color(0f, 0f, 0f, 0.72f));
        SetAnchors(dialoguePanelGO.GetComponent<RectTransform>(),
            new Vector2(0.04f, 0.36f), new Vector2(0.96f, 0.94f));

        var eventTitleGO = CreateTMP("TitleText", dialoguePanelGO.transform,
            "", 26, TextAlignmentOptions.TopLeft, new Color(0.95f, 0.85f, 0.3f));
        SetAnchors(eventTitleGO.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.84f), new Vector2(0.98f, 1f));

        var narrativeGO = CreateTMP("NarrativeText", dialoguePanelGO.transform,
            "", 20, TextAlignmentOptions.TopLeft, Color.white);
        SetAnchors(narrativeGO.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.82f));

        var dialogueComp = dialoguePanelGO.AddComponent<DialoguePanel>();
        dialogueComp.titleText     = eventTitleGO.GetComponent<TextMeshProUGUI>();
        dialogueComp.narrativeText = narrativeGO.GetComponent<TextMeshProUGUI>();

        // ── ChoiceContainer ───────────────────────────────────────────────────
        var choiceContainerRT = EmptyRect("ChoiceContainer", canvas.transform);
        SetAnchors(choiceContainerRT,
            new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.34f));
        var cvlg = choiceContainerRT.gameObject.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing = 8f;
        cvlg.childControlWidth = true;
        cvlg.childControlHeight = false;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;
        cvlg.padding = new RectOffset(0, 0, 4, 4);

        // ── Wire UIManager ────────────────────────────────────────────────────
        uiManager.dialoguePanel      = dialogueComp;
        uiManager.fadeController     = fadeCtrl;
        uiManager.choiceContainer    = choiceContainerRT;
        uiManager.choiceButtonPrefab = choiceButtonPrefab;

        EditorSceneManager.SaveScene(scene, GamePath);
        Debug.Log($"[SceneBuilder] Game → {GamePath}");
    }

    // ─── Ending Scene ─────────────────────────────────────────────────────────

    private static void BuildEndingScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();

        var canvas = CreateCanvas("Canvas");

        // Background
        var bg = CreatePanel("Background", canvas.transform, Color.black);
        SetFill(bg);

        // ── Title ─────────────────────────────────────────────────────────────
        var titleGO = CreateTMP("TitleText", canvas.transform,
            "", 44, TextAlignmentOptions.Center, new Color(0.95f, 0.85f, 0.3f));
        SetAnchors(titleGO.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f));

        // ── Body ──────────────────────────────────────────────────────────────
        var bodyGO = CreateTMP("BodyText", canvas.transform,
            "", 19, TextAlignmentOptions.TopLeft, Color.white);
        SetAnchors(bodyGO.GetComponent<RectTransform>(),
            new Vector2(0.10f, 0.46f), new Vector2(0.90f, 0.81f));

        // ── Belief Choice Panel ───────────────────────────────────────────────
        var beliefPanelGO = CreatePanel("BeliefChoicePanel", canvas.transform,
            new Color(0.07f, 0.07f, 0.07f, 0.96f));
        SetAnchors(beliefPanelGO.GetComponent<RectTransform>(),
            new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.45f));

        var choicePromptGO = CreateTMP("ChoicePromptText", beliefPanelGO.transform,
            "After experiencing those events yourself — what do you believe now?",
            19, TextAlignmentOptions.Center, Color.white);
        SetAnchors(choicePromptGO.GetComponent<RectTransform>(),
            new Vector2(0.04f, 0.62f), new Vector2(0.96f, 1f));

        var (believeBtn, believeLabelTMP) = CreateAnchoredButton(
            "BelieveButton", beliefPanelGO.transform,
            "I now believe wholeheartedly.",
            new Vector2(0.03f, 0.05f), new Vector2(0.47f, 0.58f));

        var (rationalBtn, rationalLabelTMP) = CreateAnchoredButton(
            "RationalButton", beliefPanelGO.transform,
            "I see it all as possible without God.",
            new Vector2(0.53f, 0.05f), new Vector2(0.97f, 0.58f));

        // ── Choice Result Text (hidden) ───────────────────────────────────────
        var choiceResultGO = CreateTMP("ChoiceResultText", canvas.transform,
            "", 19, TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 0.7f));
        SetAnchors(choiceResultGO.GetComponent<RectTransform>(),
            new Vector2(0.10f, 0.03f), new Vector2(0.90f, 0.11f));
        choiceResultGO.SetActive(false);

        // ── Exit Menu Panel (hidden) ──────────────────────────────────────────
        var exitMenuGO = CreatePanel("ExitMenuPanel", canvas.transform,
            new Color(0.05f, 0.05f, 0.05f, 0.97f));
        SetAnchors(exitMenuGO.GetComponent<RectTransform>(),
            new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.46f));
        exitMenuGO.SetActive(false);

        var exitQuestionGO = CreateTMP("ExitQuestionText", exitMenuGO.transform,
            "Are you having second thoughts?", 22, TextAlignmentOptions.Center, Color.white);
        SetAnchors(exitQuestionGO.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.64f), new Vector2(0.95f, 1f));

        var (restartBtn, _) = CreateAnchoredButton("RestartButton", exitMenuGO.transform,
            "Take me back", new Vector2(0.05f, 0.06f), new Vector2(0.45f, 0.60f));

        var (quitBtn, _) = CreateAnchoredButton("QuitButton", exitMenuGO.transform,
            "Quit", new Vector2(0.55f, 0.06f), new Vector2(0.95f, 0.60f));

        // ── Wire EndingController ─────────────────────────────────────────────
        var controllerGO = new GameObject("EndingController");
        var ctrl = controllerGO.AddComponent<EndingController>();
        ctrl.titleText          = titleGO.GetComponent<TextMeshProUGUI>();
        ctrl.bodyText           = bodyGO.GetComponent<TextMeshProUGUI>();
        ctrl.beliefChoicePanel  = beliefPanelGO;
        ctrl.choicePromptText   = choicePromptGO.GetComponent<TextMeshProUGUI>();
        ctrl.believeButton      = believeBtn;
        ctrl.believeButtonLabel = believeLabelTMP;
        ctrl.rationalButton     = rationalBtn;
        ctrl.rationalButtonLabel = rationalLabelTMP;
        ctrl.choiceResultText   = choiceResultGO.GetComponent<TextMeshProUGUI>();
        ctrl.exitMenuPanel      = exitMenuGO;
        ctrl.exitQuestionText   = exitQuestionGO.GetComponent<TextMeshProUGUI>();
        ctrl.restartButton      = restartBtn;
        ctrl.quitButton         = quitBtn;

        EditorSceneManager.SaveScene(scene, EndingPath);
        Debug.Log($"[SceneBuilder] Ending → {EndingPath}");
    }

    // ─── Stealth Scene ───────────────────────────────────────────────────

    private static void BuildStealthScene()
    {
        BuildGameplayScene(
            StealthPath,
            "Stealth",
            typeof(StealthSceneManager),
            isEscape: false);
        Debug.Log($"[SceneBuilder] StealthScene \u2192 {StealthPath}");
    }

    // ─── Escape Scene ───────────────────────────────────────────────────

    private static void BuildEscapeScene()
    {
        BuildGameplayScene(
            EscapePath,
            "Escape",
            typeof(EscapeSceneManager),
            isEscape: true);
        Debug.Log($"[SceneBuilder] EscapeScene \u2192 {EscapePath}");
    }

    /// <summary>
    /// Shared builder for the two top-down 2D gameplay scenes.
    /// Creates a top-down camera, a tiled garden/city floor, hedge/wall obstacles,
    /// guard patrol routes, a player start, and an exit trigger.
    /// The SceneManager component type differs between stealth and escape.
    /// </summary>
    private static void BuildGameplayScene(string scenePath, string sceneLabel,
                                           System.Type managerType, bool isEscape)
    {
        // Ensure the custom tags exist before any GameObject.tag assignment
        EnsureTag("Exit");
        EnsureTag("EscapeExit");
        EnsureTag("Player");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateEventSystem();

        // ── Top-down camera ───────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.08f, 0.03f);
        cam.orthographic    = true;
        cam.orthographicSize = 9f;   // tighter view — player always visible
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        // CameraFollow is wired to the Player after the player GO is created

        // ── Scene manager ─────────────────────────────────────────────────────
        var smGO = new GameObject(sceneLabel + "SceneManager");
        var sm   = smGO.AddComponent(managerType);

        // ── Ground plane ──────────────────────────────────────────────────────
        var ground = new GameObject("Ground");
        var groundSR = ground.AddComponent<SpriteRenderer>();
        groundSR.sprite = GetDefaultSprite();
        groundSR.color  = new Color(0.06f, 0.12f, 0.04f);
        ground.transform.localScale = new Vector3(36f, 36f, 1f);

        // ── Boundary walls ────────────────────────────────────────────────────
        CreateWall("Wall_Top",    new Vector2( 0f,  17f), new Vector2(38f, 2f));
        CreateWall("Wall_Bottom", new Vector2( 0f, -17f), new Vector2(38f, 2f));
        CreateWall("Wall_Left",   new Vector2(-19f,  0f), new Vector2(2f, 38f));
        CreateWall("Wall_Right",  new Vector2( 19f,  0f), new Vector2(2f, 38f));

        // ── Interior obstacles (hedges / market stalls) ───────────────────────
        if (!isEscape)
        {
            // Garden layout: olive tree clusters and hedge rows
            CreateWall("Hedge_1", new Vector2(-8f,  5f), new Vector2(10f, 1.5f));
            CreateWall("Hedge_2", new Vector2( 5f,  2f), new Vector2(1.5f, 8f));
            CreateWall("Hedge_3", new Vector2(-5f, -4f), new Vector2(6f,  1.5f));
            CreateWall("Hedge_4", new Vector2( 8f, -6f), new Vector2(1.5f, 6f));
            CreateWall("Hedge_5", new Vector2( 0f,  9f), new Vector2(8f,  1.5f));
        }
        else
        {
            // City street layout: buildings and alleys
            CreateWall("Block_1", new Vector2(-10f,  7f), new Vector2(7f, 5f));
            CreateWall("Block_2", new Vector2(  5f,  7f), new Vector2(7f, 5f));
            CreateWall("Block_3", new Vector2(-10f, -5f), new Vector2(7f, 5f));
            CreateWall("Block_4", new Vector2(  5f, -5f), new Vector2(7f, 5f));
            CreateWall("Block_5", new Vector2( -2f,  1f), new Vector2(5f, 3f));
        }

        // ── Player ────────────────────────────────────────────────────────────
        var playerGO  = new GameObject("Player");
        playerGO.tag  = "Player";
        playerGO.transform.position = new Vector3(-14f, -12f, 0f);
        var playerCol             = playerGO.AddComponent<CircleCollider2D>();
        playerCol.radius          = 0.4f;
        var playerRB              = playerGO.AddComponent<Rigidbody2D>();
        playerRB.gravityScale     = 0f;
        playerRB.freezeRotation   = true;
        playerGO.AddComponent<PlayerStealthController>();
        var playerSR  = playerGO.AddComponent<SpriteRenderer>();
        playerSR.sprite = GetDefaultSprite();
        playerSR.color  = new Color(0.25f, 0.55f, 1f);
        playerGO.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        // Wire camera follow now that the player GO exists
        var camFollow = camGO.AddComponent<CameraFollow>();
        camFollow.target = playerGO.transform;
        camFollow.boundsHalfExtent = new Vector2(16f, 16f);

        // ── Guards ────────────────────────────────────────────────────────────
        int guardCount = isEscape ? 5 : 3;
        CreateGuardPatrols(guardCount, isEscape);

        // ── Exit trigger ──────────────────────────────────────────────────────
        string exitTag = isEscape ? "EscapeExit" : "Exit";
        var exitGO = new GameObject("Exit");
        exitGO.tag = exitTag;
        exitGO.transform.position = new Vector3(14f, 12f, 0f);
        var exitCol         = exitGO.AddComponent<BoxCollider2D>();
        exitCol.isTrigger   = true;
        exitCol.size        = new Vector2(3f, 3f);
        var exitSR          = exitGO.AddComponent<SpriteRenderer>();
        exitSR.sprite       = GetDefaultSprite();
        exitSR.color        = new Color(1f, 0.9f, 0.1f, 0.75f);
        exitGO.transform.localScale = new Vector3(3f, 3f, 1f);

        // Arrow pointing down toward exit centre
        var arrowGO   = new GameObject("ExitArrow");
        arrowGO.transform.position = new Vector3(14f, 14.6f, 0f);
        var arrowSR   = arrowGO.AddComponent<SpriteRenderer>();
        arrowSR.sprite = GetDefaultSprite();
        arrowSR.color  = new Color(1f, 0.9f, 0.1f, 1f);
        arrowGO.transform.localScale = new Vector3(0.5f, 1.6f, 1f);
        arrowGO.AddComponent<ExitPulse>();  // added below

        // World-space label above the exit
        var exitCanvas = new GameObject("ExitCanvas");
        exitCanvas.transform.position = new Vector3(14f, 15.8f, 0f);
        var ec = exitCanvas.AddComponent<Canvas>();
        ec.renderMode = RenderMode.WorldSpace;
        ec.sortingOrder = 5;
        exitCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 1.4f);
        var exitLabelGO = CreateTMP("ExitLabel", exitCanvas.transform,
            isEscape ? "CITY GATE\n\u25BC ESCAPE HERE \u25BC" : "EXIT\n\u25BC REACH HERE \u25BC",
            0.55f, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.2f));
        FillRect(exitLabelGO);

        // ── HUD Canvas ────────────────────────────────────────────────────────
        var hudCanvas = CreateCanvas("HUD");
        var hud = BuildGameplayHUD(hudCanvas, sm, managerType, isEscape);

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void CreateGuardPatrols(int count, bool isEscape)
    {
        // Predefined patrol paths that match the obstacle layout
        Vector2[][] patrolSets = isEscape
            ? new[]
            {
                new[]{ new Vector2(-12f,10f), new Vector2(-12f,0f), new Vector2(-4f,0f), new Vector2(-4f,10f) },
                new[]{ new Vector2(  8f,10f), new Vector2( 14f,10f), new Vector2(14f,2f), new Vector2(8f,2f)  },
                new[]{ new Vector2( -4f,-8f), new Vector2( 4f,-8f), new Vector2(4f,-2f), new Vector2(-4f,-2f) },
                new[]{ new Vector2( 12f,-8f), new Vector2(16f,-8f), new Vector2(16f,-2f)                      },
                new[]{ new Vector2( -8f, 4f), new Vector2(-8f,-2f)                                            },
            }
            : new[]
            {
                new[]{ new Vector2(-14f,  8f), new Vector2(-4f,  8f), new Vector2(-4f, 0f), new Vector2(-14f, 0f) },
                new[]{ new Vector2(  2f,  6f), new Vector2( 8f,  6f), new Vector2( 8f,-4f), new Vector2(  2f,-4f) },
                new[]{ new Vector2(-10f, -7f), new Vector2( 0f, -7f), new Vector2( 0f,-12f),new Vector2(-10f,-12f) },
            };

        for (int i = 0; i < Mathf.Min(count, patrolSets.Length); i++)
        {
            var guardGO  = new GameObject($"Guard_{i + 1}");
            guardGO.transform.position = new Vector3(patrolSets[i][0].x, patrolSets[i][0].y, 0f);
            var guardCol              = guardGO.AddComponent<CapsuleCollider2D>();
            guardCol.size             = new Vector2(0.7f, 0.9f);
            var guardRB               = guardGO.AddComponent<Rigidbody2D>();
            guardRB.gravityScale      = 0f;
            guardRB.freezeRotation    = true;
            var guardSR               = guardGO.AddComponent<SpriteRenderer>();
            guardSR.sprite            = GetDefaultSprite();
            guardSR.color             = new Color(0.85f, 0.2f, 0.2f);
            guardGO.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            var guardCtrl             = guardGO.AddComponent<GuardController>();
            if (isEscape) guardCtrl.moveSpeed = 2.0f;  // faster in escape scene

            // Create waypoint GameObjects
            var waypointParent = new GameObject($"Guard_{i+1}_Waypoints");
            foreach (var wp in patrolSets[i])
            {
                var wpGO = new GameObject("Waypoint");
                wpGO.transform.SetParent(waypointParent.transform);
                wpGO.transform.position = new Vector3(wp.x, wp.y, 0f);
                guardCtrl.waypoints.Add(wpGO.transform);
            }
        }
    }

    private static GameObject CreateWall(string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position   = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.layer                = LayerMask.NameToLayer("Default");
        var sr   = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDefaultSprite();
        sr.color  = new Color(0.15f, 0.28f, 0.12f);
        var col  = go.AddComponent<BoxCollider2D>();
        return go;
    }

    private static UnityEngine.Sprite GetDefaultSprite()
    {
        // Unity's built-in white square sprite
        return UnityEditor.AssetDatabase.GetBuiltinExtraResource<UnityEngine.Sprite>("UI/Skin/UISprite.psd");
    }

    private static Component BuildGameplayHUD(Canvas hudCanvas, Component sceneManager,
                                               System.Type managerType, bool isEscape)
    {
        var hudT = hudCanvas.transform;

        // Detection bar
        var detLabel = CreateTMP("DetectionLabel", hudT, "Detection", 16,
            TextAlignmentOptions.Left, Color.white);
        SetAnchors(detLabel.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.93f), new Vector2(0.18f, 0.99f));

        var detSliderGO   = new GameObject("DetectionSlider", typeof(RectTransform));
        detSliderGO.transform.SetParent(hudT, false);
        SetAnchors(detSliderGO.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.88f), new Vector2(0.35f, 0.93f));
        var detSlider     = detSliderGO.AddComponent<Slider>();
        detSlider.minValue = 0f; detSlider.maxValue = 100f;
        var detBG = CreatePanel("Background", detSliderGO.transform, new Color(0.2f,0.2f,0.2f));
        SetFill(detBG);
        var detFillArea = new GameObject("Fill Area", typeof(RectTransform));
        detFillArea.transform.SetParent(detSliderGO.transform, false); SetFill(detFillArea);
        var detFillGO   = new GameObject("Fill",      typeof(RectTransform));
        detFillGO.transform.SetParent(detFillArea.transform, false);  SetFill(detFillGO);
        var detFillImg  = detFillGO.AddComponent<Image>();
        detFillImg.color = new Color(0.2f, 0.8f, 0.3f);
        detSlider.fillRect = detFillGO.GetComponent<RectTransform>();
        var detHandleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        detHandleArea.transform.SetParent(detSliderGO.transform, false); SetFill(detHandleArea);
        var detHandle = new GameObject("Handle", typeof(RectTransform));
        detHandle.transform.SetParent(detHandleArea.transform, false);
        detHandle.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        var detHandleImg = detHandle.AddComponent<Image>();
        detHandleImg.color = new Color(0, 0, 0, 0);  // invisible handle
        detSlider.handleRect = detHandle.GetComponent<RectTransform>();
        detSlider.targetGraphic = detHandleImg;

        // Shift bar
        var shiftLabel = CreateTMP("ShiftLabel", hudT, "Shift", 16,
            TextAlignmentOptions.Left, new Color(0.6f, 0.9f, 1f));
        SetAnchors(shiftLabel.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.82f), new Vector2(0.18f, 0.88f));

        var shiftSliderGO = new GameObject("ShiftSlider", typeof(RectTransform));
        shiftSliderGO.transform.SetParent(hudT, false);
        SetAnchors(shiftSliderGO.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.77f), new Vector2(0.35f, 0.82f));
        var shiftSlider     = shiftSliderGO.AddComponent<Slider>();
        shiftSlider.minValue = 0f; shiftSlider.maxValue = 1f; shiftSlider.value = 1f;
        var shiftBG = CreatePanel("Background", shiftSliderGO.transform, new Color(0.2f,0.2f,0.2f));
        SetFill(shiftBG);
        var shiftFillArea = new GameObject("Fill Area", typeof(RectTransform));
        shiftFillArea.transform.SetParent(shiftSliderGO.transform, false); SetFill(shiftFillArea);
        var shiftFillGO   = new GameObject("Fill",      typeof(RectTransform));
        shiftFillGO.transform.SetParent(shiftFillArea.transform, false);  SetFill(shiftFillGO);
        var shiftFillImg  = shiftFillGO.AddComponent<Image>();
        shiftFillImg.color = new Color(0.6f, 0.9f, 1f);
        shiftSlider.fillRect = shiftFillGO.GetComponent<RectTransform>();
        var shiftHandleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        shiftHandleArea.transform.SetParent(shiftSliderGO.transform, false); SetFill(shiftHandleArea);
        var shiftHandle = new GameObject("Handle", typeof(RectTransform));
        shiftHandle.transform.SetParent(shiftHandleArea.transform, false);
        shiftHandle.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        var shiftHandleImg = shiftHandle.AddComponent<Image>();
        shiftHandleImg.color = new Color(0,0,0,0);
        shiftSlider.handleRect = shiftHandle.GetComponent<RectTransform>();
        shiftSlider.targetGraphic = shiftHandleImg;

        // Instruction text
        var instrGO = CreateTMP("InstructionText", hudT,
            "", 15, TextAlignmentOptions.BottomLeft, new Color(0.85f,0.85f,0.85f));
        SetAnchors(instrGO.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0.0f), new Vector2(0.65f, 0.12f));

        // Timer (escape scene only)
        GameObject timerGO = null;
        if (isEscape)
        {
            timerGO = CreateTMP("TimerText", hudT,
                "01:30", 28, TextAlignmentOptions.Right, Color.white);
            SetAnchors(timerGO.GetComponent<RectTransform>(),
                new Vector2(0.8f, 0.92f), new Vector2(0.98f, 0.99f));
        }

        // Outcome panel (hidden)
        var outcomePanelGO = CreatePanel("OutcomePanel", hudT, new Color(0,0,0,0.85f));
        SetAnchors(outcomePanelGO.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));
        outcomePanelGO.SetActive(false);
        var outcomeTextGO = CreateTMP("OutcomeText", outcomePanelGO.transform,
            "", 20, TextAlignmentOptions.Center, Color.white);
        SetAnchors(outcomeTextGO.GetComponent<RectTransform>(),
            new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.96f));

        // Retry button (escape scene only, hidden until failure)
        Button retryBtn = null;
        if (isEscape)
        {
            var (rb, _) = CreateAnchoredButton("RetryButton", outcomePanelGO.transform,
                "Try Again", new Vector2(0.05f, 0.04f), new Vector2(0.45f, 0.22f));
            retryBtn = rb;
            rb.gameObject.SetActive(false);
        }

        // Continue button (shown after outcome is read)
        var (contBtn, _) = CreateAnchoredButton("ContinueButton", outcomePanelGO.transform,
            "Continue", new Vector2(isEscape ? 0.55f : 0.25f, 0.04f),
            new Vector2(isEscape ? 0.95f : 0.75f, 0.22f));
        contBtn.gameObject.SetActive(false);

        // Wire scene manager
        if (!isEscape && sceneManager is StealthSceneManager ssm)
        {
            ssm.detectionSlider = detSlider;
            ssm.detectionFill   = detFillImg;
            ssm.shiftSlider     = shiftSlider;
            ssm.shiftFill       = shiftFillImg;
            ssm.shiftLabel      = shiftLabel.GetComponent<TextMeshProUGUI>();
            ssm.instructionText = instrGO.GetComponent<TextMeshProUGUI>();
            ssm.outcomeText     = outcomeTextGO.GetComponent<TextMeshProUGUI>();
            ssm.outcomePanel    = outcomePanelGO;
            ssm.continueButton  = contBtn;
        }
        else if (isEscape && sceneManager is EscapeSceneManager esm)
        {
            esm.detectionSlider = detSlider;
            esm.detectionFill   = detFillImg;
            esm.shiftSlider     = shiftSlider;
            esm.shiftFill       = shiftFillImg;
            esm.instructionText = instrGO.GetComponent<TextMeshProUGUI>();
            esm.outcomeText     = outcomeTextGO.GetComponent<TextMeshProUGUI>();
            esm.outcomePanel    = outcomePanelGO;
            esm.retryButton     = retryBtn;
            esm.continueButton  = contBtn;
            if (timerGO != null)
                esm.timerText = timerGO.GetComponent<TextMeshProUGUI>();
        }

        return sceneManager;
    }

    // ─── Build Settings ───────────────────────────────────────────────────────

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuPath, true),
            new EditorBuildSettingsScene(GamePath,     true),
            new EditorBuildSettingsScene(EndingPath,   true),
            new EditorBuildSettingsScene(StealthPath,  true),
            new EditorBuildSettingsScene(EscapePath,   true),
        };

        // Always start Play mode from MainMenu regardless of which scene is open
        var mainMenuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
        EditorSceneManager.playModeStartScene = mainMenuAsset;

        Debug.Log("[SceneBuilder] Build Settings: MainMenu(0), Game(1), Ending(2), StealthScene(3), EscapeScene(4).");
    }

    // ─── UI Helpers ───────────────────────────────────────────────────────────

    private static Canvas CreateCanvas(string name)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = Color.black;
        cam.cullingMask      = 0;            // UI is drawn by Canvas; camera only clears the buffer
        cam.orthographic     = true;
        camGO.AddComponent<AudioListener>();
    }

    private static void CreateEventSystem()
    {
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateTMP(string name, Transform parent, string text,
        float fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.alignment          = align;
        tmp.color              = color;
        tmp.textWrappingMode  = TextWrappingModes.Normal;
        return go;
    }

    // Creates a full-width stacked button for use inside a VerticalLayoutGroup.
    private static Button CreateMenuButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 58);
        go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.92f);
        var btn = go.AddComponent<Button>();
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 58f;
        le.flexibleWidth = 1f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        FillRect(labelGO);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        return btn;
    }

    // Creates a button sized by anchor min/max (not layout group).
    private static (Button btn, TMP_Text label) CreateAnchoredButton(
        string name, Transform parent, string labelText,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.sizeDelta       = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.92f);
        var btn = go.AddComponent<Button>();

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        FillRect(labelGO);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text               = labelText;
        tmp.fontSize           = 17;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.color              = Color.white;
        tmp.textWrappingMode  = TextWrappingModes.Normal;
        return (btn, tmp);
    }

    private static TMP_InputField CreateTMPInputField(string name, Transform parent)
    {
        var rootGO = new GameObject(name);
        rootGO.transform.SetParent(parent, false);
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(0, 52);
        var le = rootGO.AddComponent<LayoutElement>();
        le.minHeight = 52f;
        le.flexibleWidth = 1f;
        rootGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f);

        // Text Area (viewport)
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(rootGO.transform, false);
        var textAreaRT = textAreaGO.AddComponent<RectTransform>();
        textAreaRT.anchorMin  = Vector2.zero;
        textAreaRT.anchorMax  = Vector2.one;
        textAreaRT.sizeDelta  = Vector2.zero;
        textAreaRT.offsetMin  = new Vector2(10, 6);
        textAreaRT.offsetMax  = new Vector2(-10, -6);
        textAreaGO.AddComponent<RectMask2D>();

        // Placeholder
        var placeholderGO = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGO.transform.SetParent(textAreaGO.transform, false);
        FillRect(placeholderGO);
        var placeholderTMP = placeholderGO.AddComponent<TextMeshProUGUI>();
        placeholderTMP.text       = "Your name…";
        placeholderTMP.fontSize   = 19;
        placeholderTMP.color      = new Color(0.5f, 0.5f, 0.5f);
        placeholderTMP.fontStyle  = FontStyles.Italic;

        // Text
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(textAreaGO.transform, false);
        FillRect(textGO);
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize = 19;
        textTMP.color    = Color.white;

        var inputField = rootGO.AddComponent<TMP_InputField>();
        inputField.textViewport  = textAreaRT;
        inputField.textComponent = textTMP;
        inputField.placeholder   = placeholderTMP;

        return inputField;
    }

    private static (Slider slider, TMP_Text label) CreateSliderRow(
        string labelText, Transform parent, float defaultValue)
    {
        var rowGO = new GameObject(labelText + "Row");
        rowGO.transform.SetParent(parent, false);
        var rowRT = rowGO.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, 36);
        var le = rowGO.AddComponent<LayoutElement>();
        le.minHeight = 36f;
        le.flexibleWidth = 1f;
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childControlHeight  = false;
        hlg.childControlWidth   = false;
        hlg.childForceExpandWidth = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // Text label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(rowGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(100, 36);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = labelText;
        labelTMP.fontSize  = 18;
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelTMP.color     = Color.white;

        // Slider
        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(rowGO.transform, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.sizeDelta = new Vector2(220, 36);
        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = defaultValue;

        // Slider background
        var sliderBG = new GameObject("Background");
        sliderBG.transform.SetParent(sliderGO.transform, false);
        var sliderBGRT = sliderBG.AddComponent<RectTransform>();
        sliderBGRT.anchorMin = new Vector2(0f, 0.3f);
        sliderBGRT.anchorMax = new Vector2(1f, 0.7f);
        sliderBGRT.sizeDelta = Vector2.zero;
        sliderBG.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

        // Fill area + fill
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin      = new Vector2(0f, 0.3f);
        fillAreaRT.anchorMax      = new Vector2(1f, 0.7f);
        fillAreaRT.sizeDelta      = new Vector2(-10, 0);
        fillAreaRT.anchoredPosition = new Vector2(-5, 0);
        var fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        FillRect(fillGO);
        fillGO.AddComponent<Image>().color = new Color(0.35f, 0.65f, 1f);
        slider.fillRect = fillGO.GetComponent<RectTransform>();

        // Handle slide area + handle
        var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        FillRect(handleAreaGO);
        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20, 20);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        slider.handleRect     = handleRT;
        slider.targetGraphic  = handleImg;

        // Value label
        var valueLabelGO = new GameObject("ValueLabel");
        valueLabelGO.transform.SetParent(rowGO.transform, false);
        var valueLabelRT = valueLabelGO.AddComponent<RectTransform>();
        valueLabelRT.sizeDelta = new Vector2(50, 36);
        var valueTMP = valueLabelGO.AddComponent<TextMeshProUGUI>();
        valueTMP.text      = Mathf.RoundToInt(defaultValue * 100) + "%";
        valueTMP.fontSize  = 16;
        valueTMP.alignment = TextAlignmentOptions.Center;
        valueTMP.color     = Color.white;

        return (slider, valueTMP);
    }

    private static RectTransform EmptyRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static void SetFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void FillRect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts   = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    /// <summary>Adds a tag to the project's TagManager if it doesn't already exist.</summary>
    private static void EnsureTag(string tagName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/TagManager.asset"));
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName) return;

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
        tagManager.ApplyModifiedProperties();
    }
}
