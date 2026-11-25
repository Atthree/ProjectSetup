using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Atthree.ProjectSetup
{
    public enum InputHandling
    {
        /// <summary>Don't change it.</summary>
        Keep = -1,
        /// <summary>Old Input Manager (UnityEngine.Input).</summary>
        InputManager = 0,
        /// <summary>Input System package only.</summary>
        InputSystem = 1,
        /// <summary>Both: old code keeps working, new code can use the Input System.</summary>
        Both = 2,
    }

    /// <summary>
    /// Everything the Project Setup window does, in one place. The window keeps it in EditorPrefs, so
    /// the same choices are ready in your next project.
    /// </summary>
    public class SetupProfile : ScriptableObject
    {
        public const string PrefsKey = "Atthree.ProjectSetup.Profile";

        [Header("Project")]
        public string companyName = "";
        public string productName = "";
        [Tooltip("Set the application id (com.company.product) for Standalone, Android and iOS.")]
        public bool setApplicationId = true;
        [Tooltip("New scripts made with Create > C# Script get this namespace. Empty = Company.Product.")]
        public string rootNamespace = "";

        [Header("Folders")]
        [Tooltip("Your own files go under Assets/<this>, away from Asset Store packs. Empty = straight into Assets.")]
        public string rootFolder = "_Project";
        public List<string> folders = new List<string>();
        [Tooltip("Put an empty .gitkeep file in empty folders. Git doesn't store empty folders, so without it they're missing on other computers.")]
        public bool addGitkeep = true;

        [Header("Packages")]
        public List<PackageEntry> packages = new List<PackageEntry>();
        [Tooltip("Install the newest version of the Atthree packages instead of the tagged one.")]
        public bool atthreeLatest;

        [Header("Settings")]
        public bool linearColorSpace = true;
        public bool forceTextSerialization = true;
        public bool visibleMetaFiles = true;
        public InputHandling inputHandling = InputHandling.Both;

        [Header("Git")]
        public bool writeGitignore = true;
        public bool writeGitattributes = true;
        [Tooltip("Store images, models and sounds with Git LFS (needs git lfs installed).")]
        public bool useGitLfs;
        [TextArea(2, 6)]
        [Tooltip("Lines added to the end of .gitignore, e.g. paid Asset Store packs you may not publish.")]
        public string extraIgnoreLines = "";
        [Tooltip("Run 'git init' if the project isn't a git repository yet.")]
        public bool gitInit = true;
        [Tooltip("Replace .gitignore / .gitattributes if they already exist.")]
        public bool overwriteGitFiles;

        [Header("Scenes")]
        public bool createScenes = true;
        public List<string> scenes = new List<string>();
        public bool addScenesToBuild = true;

        [Header("Window")]
        [Tooltip("Open this window once in every project that doesn't have the root folder yet.")]
        public bool showInNewProjects = true;

        public static readonly string[] DefaultFolders =
        {
            "Art/Animations",
            "Art/Materials",
            "Art/Models",
            "Art/Textures",
            "Audio/Music",
            "Audio/SFX",
            "Prefabs",
            "Scenes",
            "Scripts/Runtime",
            "Scripts/Editor",
            "ScriptableObjects",
            "Settings",
            "UI",
            "VFX",
        };

        public static readonly string[] DefaultScenes = { "MainMenu", "Game" };

        public static SetupProfile CreateDefault()
        {
            var p = CreateInstance<SetupProfile>();
            p.hideFlags = HideFlags.DontSave;
            p.ResetToDefaults();
            return p;
        }

        public void ResetToDefaults()
        {
            var fresh = CreateInstance<SetupProfile>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fresh), this);
            DestroyImmediate(fresh);
            folders = new List<string>(DefaultFolders);
            scenes = new List<string>(DefaultScenes);
            packages = PackageCatalog.CreateDefault();
            companyName = PlayerSettings.companyName;
            productName = PlayerSettings.productName;
        }

        /// <summary>Folder of the project's scenes, e.g. Assets/_Project/Scenes.</summary>
        public string ScenesFolder => SetupPaths.Combine(RootPath, "Scenes");

        /// <summary>Assets or Assets/&lt;rootFolder&gt;.</summary>
        public string RootPath => SetupPaths.Combine("Assets", SetupPaths.Clean(rootFolder));

        public string EffectiveNamespace =>
            string.IsNullOrWhiteSpace(rootNamespace)
                ? SetupNames.ToNamespace(companyName, productName)
                : rootNamespace.Trim();

        // ---------------------------------------------------------------- saving

        public void SaveToPrefs(string key = PrefsKey) => EditorPrefs.SetString(key, EditorJsonUtility.ToJson(this));

        /// <summary>Loads the saved choices. Company and product always come from this project.</summary>
        public static SetupProfile LoadFromPrefs(string key = PrefsKey)
        {
            var p = CreateDefault();
            var json = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return p;

            EditorJsonUtility.FromJsonOverwrite(json, p);
            p.hideFlags = HideFlags.DontSave;
            p.companyName = PlayerSettings.companyName;
            p.productName = PlayerSettings.productName;
            p.MergeNewCatalogEntries();
            return p;
        }

        /// <summary>Packages added to the catalog after the choices were saved show up too.</summary>
        void MergeNewCatalogEntries()
        {
            if (packages == null) packages = new List<PackageEntry>();
            foreach (var entry in PackageCatalog.CreateDefault())
            {
                var existing = packages.Find(p => p.id == entry.id);
                if (existing == null)
                {
                    entry.selected = false;
                    packages.Add(entry);
                }
                else if (existing.source == PackageSource.Git && entry.source == PackageSource.Git)
                {
                    // Keep the user's tick, but use the newest known tag and URL.
                    existing.gitUrl = entry.gitUrl;
                    existing.version = entry.version;
                }
            }
        }
    }
}
