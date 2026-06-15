using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click utility: scans Assets/Images/StoryImages/ and creates or refreshes
/// a StoryImageLibrary ScriptableObject at Assets/Resources/StoryImageLibrary.asset.
///
/// Run via:  Tools → Setup Story Images
/// (Also invoked automatically by SceneBuilder → Build All Scenes.)
/// </summary>
public static class AutoSetupStoryImages
{
    private const string ImagesRoot  = "Assets/Images/StoryImages";
    private const string LibraryPath = "Assets/Resources/StoryImageLibrary.asset";

    private static readonly string[] EventIds =
        { "event_0", "event_1", "event_2", "event_3",
          "event_4", "event_5", "event_6", "event_7" };

    [MenuItem("Tools/Setup Story Images")]
    public static void SetupStoryImages()
    {
        // Ensure the Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Load existing asset or create a new one
        var library = AssetDatabase.LoadAssetAtPath<StoryImageLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<StoryImageLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // ── Main-menu background ────────────────────────────────────────────
        library.mainMenuBackground =
            LoadSprite("mainmenu/mainmenu.png");

        // ── Per-event sets ──────────────────────────────────────────────────
        var sets = new List<StoryImageLibrary.EventImageSet>();
        for (int i = 0; i < EventIds.Length; i++)
        {
            string folderName = "event" + i;   // event0, event1, …
            string prefix     = "event" + i;   // file-name prefix

            // Determine how many frames go to the slideshow vs. postChoiceFrames
            // by reading the event JSON's slideshowTexts count.
            string jsonPath = $"Assets/Resources/Events/{EventIds[i]}.json";
            int slideCount  = GetSlideshowTextsCount(jsonPath);
            var (slideFrames, postFrames) = LoadSequenceSplit(folderName, prefix + "_", slideCount, maxCount: 10);

            var set = new StoryImageLibrary.EventImageSet
            {
                eventId          = EventIds[i],
                slideshowFrames  = slideFrames,
                postChoiceFrames = postFrames,
                choiceImages     = LoadChoices(folderName, prefix + "_choice", maxCount: 5)
            };
            sets.Add(set);
        }
        library.events = sets.ToArray();

        // ── Choice background ───────────────────────────────────────────────
        library.choiceBackground = LoadSprite("choice/choicebackground.png");

        // ── Ending images ───────────────────────────────────────────────────
        var endings = new List<Sprite>();
        for (int i = 0; i <= 3; i++)
        {
            var s = LoadSprite($"ending/ending_{i}.png");
            if (s != null) endings.Add(s);
        }
        library.endingImages = endings.ToArray();

        // ── Save ────────────────────────────────────────────────────────────
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AutoSetupStoryImages] StoryImageLibrary created at {LibraryPath}  " +
                  $"({library.events.Length} events, {library.endingImages.Length} ending images).");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static int GetSlideshowTextsCount(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return 0;
        var evt = JsonUtility.FromJson<NarrativeEvent>(File.ReadAllText(jsonPath));
        return evt?.slideshowTexts?.Count ?? 0;
    }

    /// <summary>
    /// Loads all numbered frames then splits at <paramref name="splitAt"/>.
    /// If splitAt is 0, all frames go to the slide array.
    /// </summary>
    private static (Sprite[] slide, Sprite[] post) LoadSequenceSplit(
        string folder, string prefix, int splitAt, int maxCount)
    {
        var all = new List<Sprite>();
        for (int n = 1; n <= maxCount; n++)
        {
            var s = LoadSprite($"{folder}/{prefix}{n}.png");
            if (s == null) break;
            all.Add(s);
        }
        if (splitAt <= 0 || splitAt >= all.Count)
            return (all.ToArray(), new Sprite[0]);
        return (all.GetRange(0, splitAt).ToArray(),
                all.GetRange(splitAt, all.Count - splitAt).ToArray());
    }

    /// <summary>
    /// Loads choice images {prefix}1 … {prefix}maxCount, keeping nulls so that
    /// indices align with choice button indices (0-based).
    /// Trailing nulls are trimmed; if nothing found returns empty array.
    /// </summary>
    private static Sprite[] LoadChoices(string folder, string prefix, int maxCount)
    {
        var sprites = new Sprite[maxCount];
        bool anyFound = false;
        for (int n = 1; n <= maxCount; n++)
        {
            var s = LoadSprite($"{folder}/{prefix}{n}.png");
            sprites[n - 1] = s;
            if (s != null) anyFound = true;
        }
        if (!anyFound) return new Sprite[0];

        // Trim trailing nulls
        int last = maxCount - 1;
        while (last >= 0 && sprites[last] == null) last--;
        if (last < 0) return new Sprite[0];

        var result = new Sprite[last + 1];
        for (int n = 0; n <= last; n++) result[n] = sprites[n];
        return result;
    }

    private static Sprite LoadSprite(string relativePath)
        => AssetDatabase.LoadAssetAtPath<Sprite>($"{ImagesRoot}/{relativePath}");
}
