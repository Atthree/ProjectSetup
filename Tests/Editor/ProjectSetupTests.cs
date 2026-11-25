using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Atthree.ProjectSetup.Tests
{
    public class ProjectSetupTests
    {
        SetupProfile profile;
        SetupReport report;
        string root;     // e.g. __PST_1234 (inside Assets)
        string tempDir;  // outside the project, for git files

        // project settings the tests change, restored afterwards
        string company, product, ns, appStandalone, appAndroid, appIos, vcMode;
        ColorSpace colorSpace;
        SerializationMode serialization;
        EditorBuildSettingsScene[] buildScenes;

        [SetUp]
        public void SetUp()
        {
            profile = SetupProfile.CreateDefault();
            report = new SetupReport();
            root = "__PST_" + Random.Range(1000, 9999);
            profile.rootFolder = root;
            tempDir = Path.Combine(Path.GetTempPath(), "pst_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);

            company = PlayerSettings.companyName;
            product = PlayerSettings.productName;
            ns = EditorSettings.projectGenerationRootNamespace;
            appStandalone = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            appAndroid = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            appIos = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            vcMode = VersionControlSettings.mode;
            colorSpace = PlayerSettings.colorSpace;
            serialization = EditorSettings.serializationMode;
            buildScenes = EditorBuildSettings.scenes;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerSettings.companyName = company;
            PlayerSettings.productName = product;
            EditorSettings.projectGenerationRootNamespace = ns;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, appStandalone);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, appAndroid);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, appIos);
            VersionControlSettings.mode = vcMode;
            PlayerSettings.colorSpace = colorSpace;
            EditorSettings.serializationMode = serialization;
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.DeleteAsset("Assets/" + root);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Object.DestroyImmediate(profile);
        }

        // ---------------------------------------------------------------- names and paths

        [TestCase("My Company", "Super Game 2", "com.mycompany.supergame2")]
        [TestCase("atthree", "shatter-hall!", "com.atthree.shatterHall")]
        [TestCase("", "", "com.company.game")]
        [TestCase("3 Kings", "Çay Ocağı", "com.a_3kings.ayoca")]
        public void ApplicationId_IsSafe(string c, string p, string expectedCaseInsensitive)
        {
            var id = SetupNames.ToApplicationId(c, p);
            Assert.AreEqual(expectedCaseInsensitive.ToLowerInvariant(), id);
            StringAssert.IsMatch(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", id);
        }

        [Test]
        public void Namespace_IsPascalCase()
        {
            Assert.AreEqual("MyCompany.SuperGame2", SetupNames.ToNamespace("my company", "super game 2"));
            Assert.AreEqual("Company._2Fast", SetupNames.ToNamespace("", "2 fast"));
        }

        [TestCase("Art/Models", "Art/Models")]
        [TestCase(" /Art\\Models/ ", "Art/Models")]
        [TestCase("../Outside", "")]
        [TestCase("Art/../x", "")]
        [TestCase("Hidden~", "")]
        [TestCase(".git", "")]
        [TestCase("Bad|Name", "")]
        [TestCase("   ", "")]
        public void CleanPath(string input, string expected)
        {
            Assert.AreEqual(expected, SetupPaths.Clean(input));
        }

        // ---------------------------------------------------------------- folders

        [Test]
        public void CreateFolders_CreatesNestedFolders_AndIsSafeToRunAgain()
        {
            profile.folders = new List<string> { "Art/Models", "Scripts/Runtime", "../Escape", "" };
            SetupSteps.CreateFolders(profile, report);

            Assert.IsTrue(AssetDatabase.IsValidFolder($"Assets/{root}/Art/Models"));
            Assert.IsTrue(AssetDatabase.IsValidFolder($"Assets/{root}/Scripts/Runtime"));
            Assert.IsTrue(AssetDatabase.IsValidFolder($"Assets/{root}/Scenes"), "scenes folder comes with Create Scenes");
            Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Escape"));
            Assert.IsFalse(Directory.Exists(Path.Combine(SetupPaths.ProjectRoot, "Escape")));
            Assert.AreEqual(1, report.Count(SetupReport.Kind.Warning), "the ../ folder is reported");
            Assert.IsTrue(File.Exists($"Assets/{root}/Art/Models.meta"), "folders get .meta files");
            Assert.IsTrue(File.Exists($"Assets/{root}/Art/Models/.gitkeep"), "empty folders get .gitkeep");
            Assert.IsFalse(File.Exists($"Assets/{root}/Art/.gitkeep"), "folders with something inside don't");

            report.Clear();
            SetupSteps.CreateFolders(profile, report);
            Assert.AreEqual(0, report.Count(SetupReport.Kind.Done), "second run creates nothing");
            Assert.AreEqual(1, report.Count(SetupReport.Kind.Skipped));
        }

        [Test]
        public void DefaultFolders_AreAllValid()
        {
            foreach (var f in SetupProfile.DefaultFolders) Assert.AreEqual(f, SetupPaths.Clean(f));
        }

        // ---------------------------------------------------------------- git

        [Test]
        public void GitFiles_AreWritten_AndNotOverwrittenUnlessAsked()
        {
            profile.extraIgnoreLines = "/Assets/Synty/\r\n/Assets/Plugins/Paid/";
            SetupSteps.WriteGitFiles(profile, report, tempDir);

            var ignore = File.ReadAllText(Path.Combine(tempDir, ".gitignore"));
            StringAssert.Contains("/[Ll]ibrary/", ignore);
            StringAssert.Contains("*.csproj", ignore);
            StringAssert.Contains("/Assets/Synty/\n/Assets/Plugins/Paid/\n", ignore);
            StringAssert.DoesNotContain("\r", ignore);
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, ".gitattributes")));
            Assert.AreEqual(2, report.Count(SetupReport.Kind.Done));

            // a file you edited yourself is kept…
            File.WriteAllText(Path.Combine(tempDir, ".gitignore"), "mine");
            report.Clear();
            SetupSteps.WriteGitFiles(profile, report, tempDir);
            Assert.AreEqual("mine", File.ReadAllText(Path.Combine(tempDir, ".gitignore")));
            Assert.AreEqual(0, report.Count(SetupReport.Kind.Done));

            // …unless Overwrite is ticked
            profile.overwriteGitFiles = true;
            report.Clear();
            SetupSteps.WriteGitFiles(profile, report, tempDir);
            Assert.AreEqual(ignore, File.ReadAllText(Path.Combine(tempDir, ".gitignore")));
        }

        [Test]
        public void Gitattributes_LfsOrBinary()
        {
            var plain = GitFiles.Gitattributes(false);
            StringAssert.Contains("*.png binary", plain);
            StringAssert.Contains("*.unity text eol=lf", plain);
            StringAssert.DoesNotContain("lfs", plain);

            var lfs = GitFiles.Gitattributes(true);
            StringAssert.Contains("*.fbx filter=lfs diff=lfs merge=lfs -text", lfs);
            StringAssert.Contains("*.prefab text eol=lf", lfs, "YAML assets never go to LFS");
        }

        [Test]
        public void GitInit_SkipsExistingRepository()
        {
            Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
            SetupSteps.GitInit(report, tempDir);
            Assert.AreEqual(1, report.Count(SetupReport.Kind.Skipped));
        }

        // ---------------------------------------------------------------- packages

        [Test]
        public void PackagePlan_OnlyTickedAndMissing()
        {
            foreach (var e in profile.packages) e.selected = false;
            profile.packages.First(e => e.id == "com.atthree.environment").selected = true;
            profile.packages.First(e => e.id == "com.unity.inputsystem").selected = true;
            profile.packages.First(e => e.id == "com.unity.probuilder").selected = true;

            var plan = SetupSteps.PlanPackages(profile, new HashSet<string> { "com.unity.probuilder" });
            CollectionAssert.AreEqual(new[]
            {
                "https://github.com/Atthree/Environment.git#v0.1.0",
                "com.unity.inputsystem",
            }, plan);

            profile.atthreeLatest = true;
            plan = SetupSteps.PlanPackages(profile, new HashSet<string>());
            Assert.Contains("https://github.com/Atthree/Environment.git", plan);
        }

        [Test]
        public void Catalog_HasUniqueIds_AndValidGitUrls()
        {
            var all = PackageCatalog.CreateDefault();
            Assert.AreEqual(all.Count, all.Select(e => e.id).Distinct().Count());
            foreach (var e in all.Where(e => e.source == PackageSource.Git))
            {
                StringAssert.StartsWith("https://github.com/Atthree/", e.gitUrl);
                StringAssert.EndsWith(".git", e.gitUrl);
                StringAssert.IsMatch(@"^v\d+\.\d+\.\d+$", e.version);
            }
        }

        [Test]
        public void InstalledPackages_IncludesThisPackage()
        {
            Assert.IsTrue(SetupSteps.InstalledPackageIds().Contains("com.atthree.project-setup"));
        }

        // ---------------------------------------------------------------- settings

        [Test]
        public void ProjectInfo_SetsNamesIdsAndNamespace()
        {
            profile.companyName = "Test Studio";
            profile.productName = "Rock Game";
            profile.rootNamespace = "";
            SetupSteps.ApplyProjectInfo(profile, report);

            Assert.AreEqual("Test Studio", PlayerSettings.companyName);
            Assert.AreEqual("Rock Game", PlayerSettings.productName);
            Assert.AreEqual("com.teststudio.rockgame", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            Assert.AreEqual("com.teststudio.rockgame", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone));
            Assert.AreEqual("TestStudio.RockGame", EditorSettings.projectGenerationRootNamespace);

            report.Clear();
            SetupSteps.ApplyProjectInfo(profile, report);
            Assert.AreEqual(0, report.lines.Count, "nothing to do the second time");
        }

        [Test]
        public void Settings_AreApplied()
        {
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            EditorSettings.serializationMode = SerializationMode.Mixed;
            VersionControlSettings.mode = "Hidden Meta Files";
            profile.inputHandling = InputHandling.Keep;

            SetupSteps.ApplySettings(profile, report);

            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
            Assert.AreEqual(SerializationMode.ForceText, EditorSettings.serializationMode);
            Assert.AreEqual("Visible Meta Files", VersionControlSettings.mode);
            Assert.IsFalse(report.restartRequired);
        }

        [Test]
        public void InputHandling_CanBeReadAndChanged()
        {
            var before = SetupSteps.GetInputHandling();
            Assert.AreNotEqual(InputHandling.Keep, before, "Player Settings has activeInputHandler");
            try
            {
                var other = before == InputHandling.Both ? InputHandling.InputManager : InputHandling.Both;
                Assert.IsTrue(SetupSteps.SetInputHandling(other));
                Assert.AreEqual(other, SetupSteps.GetInputHandling());
                Assert.IsFalse(SetupSteps.SetInputHandling(other), "same value: no change");
            }
            finally
            {
                SetupSteps.SetInputHandling(before);
            }
        }

        // ---------------------------------------------------------------- scenes

        [Test]
        public void Scenes_AreCreatedAndAddedToBuild_Once()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            profile.scenes = new List<string> { "Menu", "Level 1", "bad/name", "Menu" };

            var paths = SetupSteps.CreateScenes(profile, report);

            CollectionAssert.AreEqual(new[] { $"Assets/{root}/Scenes/Menu.unity", $"Assets/{root}/Scenes/Level 1.unity" }, paths);
            foreach (var p in paths) Assert.IsTrue(File.Exists(p), p);
            Assert.AreEqual(1, report.Count(SetupReport.Kind.Warning), "bad/name is skipped");

            var menu = EditorSceneManager.OpenScene(paths[0]);
            Assert.IsTrue(menu.GetRootGameObjects().Any(g => g.GetComponent<Camera>() != null), "scene has a camera");

            int inBuild = EditorBuildSettings.scenes.Count(s => paths.Contains(s.path));
            Assert.AreEqual(2, inBuild);

            report.Clear();
            SetupSteps.CreateScenes(profile, report);
            Assert.AreEqual(2, EditorBuildSettings.scenes.Count(s => paths.Contains(s.path)), "no duplicates in the build list");
            Assert.AreEqual(0, report.Count(SetupReport.Kind.Done));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ---------------------------------------------------------------- profile

        [Test]
        public void Profile_RoundTripsThroughEditorPrefs()
        {
            const string key = "Atthree.ProjectSetup.TestProfile";
            try
            {
                profile.rootFolder = "_Game";
                profile.folders = new List<string> { "A", "B/C" };
                profile.useGitLfs = true;
                profile.packages.First(e => e.id == "com.unity.splines").selected = true;
                profile.packages.RemoveAll(e => e.id == "com.unity.recorder"); // as if saved by an older version
                profile.SaveToPrefs(key);

                var loaded = SetupProfile.LoadFromPrefs(key);
                Assert.AreEqual("_Game", loaded.rootFolder);
                CollectionAssert.AreEqual(new[] { "A", "B/C" }, loaded.folders);
                Assert.IsTrue(loaded.useGitLfs);
                Assert.IsTrue(loaded.packages.First(e => e.id == "com.unity.splines").selected);
                var recorder = loaded.packages.FirstOrDefault(e => e.id == "com.unity.recorder");
                Assert.IsNotNull(recorder, "new catalog entries are added");
                Assert.IsFalse(recorder.selected, "…but not ticked");
                Assert.AreEqual(PlayerSettings.productName, loaded.productName, "names come from this project");
                Object.DestroyImmediate(loaded);
            }
            finally
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void ResetToDefaults_RestoresLists()
        {
            profile.folders.Clear();
            profile.packages.Clear();
            profile.rootFolder = "x";
            profile.ResetToDefaults();
            Assert.AreEqual(SetupProfile.DefaultFolders.Length, profile.folders.Count);
            Assert.AreEqual(PackageCatalog.CreateDefault().Count, profile.packages.Count);
            Assert.AreEqual("_Project", profile.rootFolder);
        }
    }
}
