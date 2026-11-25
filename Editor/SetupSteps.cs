using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Atthree.ProjectSetup
{
    /// <summary>
    /// The setup steps. Each one can run on its own, is safe to run again and writes what it did into a
    /// <see cref="SetupReport"/>. The window calls them; you can also call them from your own editor code.
    /// </summary>
    public static class SetupSteps
    {
        // ================================================================ project info

        public static void ApplyProjectInfo(SetupProfile p, SetupReport r)
        {
            if (!string.IsNullOrWhiteSpace(p.companyName) && PlayerSettings.companyName != p.companyName.Trim())
            {
                PlayerSettings.companyName = p.companyName.Trim();
                r.Done($"Company name: {PlayerSettings.companyName}");
            }
            if (!string.IsNullOrWhiteSpace(p.productName) && PlayerSettings.productName != p.productName.Trim())
            {
                PlayerSettings.productName = p.productName.Trim();
                r.Done($"Product name: {PlayerSettings.productName}");
            }

            if (p.setApplicationId)
            {
                var id = SetupNames.ToApplicationId(PlayerSettings.companyName, PlayerSettings.productName);
                bool changed = false;
                foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
                {
                    if (PlayerSettings.GetApplicationIdentifier(target) == id) continue;
                    PlayerSettings.SetApplicationIdentifier(target, id);
                    changed = true;
                }
                if (changed) r.Done($"Application id: {id}");
            }

            var ns = p.EffectiveNamespace;
            if (EditorSettings.projectGenerationRootNamespace != ns)
            {
                EditorSettings.projectGenerationRootNamespace = ns;
                r.Done($"Root namespace for new scripts: {ns}");
            }
        }

        // ================================================================ settings

        public static void ApplySettings(SetupProfile p, SetupReport r)
        {
            if (p.linearColorSpace && PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                r.Done("Color space: Linear");
            }
            if (p.forceTextSerialization && EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                r.Done("Asset serialization: Force Text (readable diffs in git)");
            }
            if (p.visibleMetaFiles && VersionControlSettings.mode != "Visible Meta Files")
            {
                VersionControlSettings.mode = "Visible Meta Files";
                r.Done("Version control: Visible Meta Files");
            }
            if (p.inputHandling != InputHandling.Keep && SetInputHandling(p.inputHandling))
            {
                r.Done($"Active input handling: {p.inputHandling} (takes effect after restarting Unity)");
                r.restartRequired = true;
            }
        }

        /// <summary>Player Settings > Active Input Handling. Returns true if it changed.</summary>
        public static bool SetInputHandling(InputHandling value)
        {
            var so = PlayerSettingsObject();
            var prop = so?.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == (int)value) return false;
            prop.intValue = (int)value;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return true;
        }

        public static InputHandling GetInputHandling()
        {
            var prop = PlayerSettingsObject()?.FindProperty("activeInputHandler");
            return prop == null ? InputHandling.Keep : (InputHandling)prop.intValue;
        }

        static SerializedObject PlayerSettingsObject()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            return asset == null ? null : new SerializedObject(asset);
        }

        // ================================================================ folders

        public static void CreateFolders(SetupProfile p, SetupReport r)
        {
            var root = p.RootPath;
            var all = new List<string>();
            if (root != "Assets") all.Add(root);
            foreach (var f in p.folders)
            {
                var clean = SetupPaths.Clean(f);
                if (clean.Length == 0)
                {
                    if (!string.IsNullOrWhiteSpace(f)) r.Warning($"Skipped folder name \"{f}\" (not a valid folder name)");
                    continue;
                }
                all.Add(SetupPaths.Combine(root, clean));
            }
            if (p.createScenes) all.Add(p.ScenesFolder);

            int created = 0, existed = 0;
            foreach (var path in all.Distinct())
            {
                if (EnsureFolder(path)) created++;
                else existed++;
            }
            if (p.addGitkeep)
            {
                int kept = 0;
                foreach (var path in all.Distinct())
                    if (AddGitkeepIfEmpty(path)) kept++;
                if (kept > 0) r.Done($"Added .gitkeep to {kept} empty folder(s)");
            }
            if (created > 0) r.Done($"Created {created} folder(s) under {root}");
            if (existed > 0) r.Skipped($"{existed} folder(s) already existed");
        }

        /// <summary>Writes .gitkeep into a folder that has nothing in it (Unity ignores files that start with a dot).</summary>
        public static bool AddGitkeepIfEmpty(string assetPath)
        {
            if (!Directory.Exists(assetPath) || Directory.EnumerateFileSystemEntries(assetPath).Any()) return false;
            File.WriteAllText(Path.Combine(assetPath, ".gitkeep"), "");
            return true;
        }

        /// <summary>Creates Assets/a/b/c one level at a time. Returns false if it already existed.</summary>
        public static bool EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return false;
            var parts = assetPath.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return true;
        }

        // ================================================================ git

        public static void WriteGitFiles(SetupProfile p, SetupReport r, string directory = null)
        {
            directory ??= SetupPaths.ProjectRoot;
            if (p.writeGitignore)
                WriteFile(Path.Combine(directory, ".gitignore"), GitFiles.Gitignore(p.extraIgnoreLines), p.overwriteGitFiles, r);
            if (p.writeGitattributes)
                WriteFile(Path.Combine(directory, ".gitattributes"), GitFiles.Gitattributes(p.useGitLfs), p.overwriteGitFiles, r);
        }

        static void WriteFile(string path, string content, bool overwrite, SetupReport r)
        {
            var name = Path.GetFileName(path);
            if (File.Exists(path))
            {
                if (File.ReadAllText(path) == content) { r.Skipped($"{name} is already up to date"); return; }
                if (!overwrite) { r.Skipped($"{name} already exists (tick Overwrite to replace it)"); return; }
            }
            File.WriteAllText(path, content);
            r.Done($"Wrote {name}");
        }

        public static void GitInit(SetupReport r, string directory = null)
        {
            directory ??= SetupPaths.ProjectRoot;
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                r.Skipped("Git repository already exists");
                return;
            }
            try
            {
                var psi = new ProcessStartInfo("git", "init")
                {
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi);
                proc.WaitForExit(15000);
                if (proc.ExitCode == 0) r.Done("git init");
                else r.Error("git init failed: " + proc.StandardError.ReadToEnd().Trim());
            }
            catch (Exception e)
            {
                r.Warning("Couldn't run git (is it installed and on PATH?): " + e.Message);
            }
        }

        // ================================================================ scenes

        /// <summary>
        /// Creates the starter scenes (camera + light) in the scenes folder and adds them to the build.
        /// The scenes you had open stay open. Save your open scenes first: the window asks for you.
        /// </summary>
        public static List<string> CreateScenes(SetupProfile p, SetupReport r)
        {
            var paths = new List<string>();
            var folder = p.ScenesFolder;
            EnsureFolder(folder);

            var toCreate = new List<string>();
            foreach (var raw in p.scenes)
            {
                var name = SetupPaths.Clean(raw);
                if (name.Length == 0 || name.Contains("/"))
                {
                    if (!string.IsNullOrWhiteSpace(raw)) r.Warning($"Skipped scene name \"{raw}\"");
                    continue;
                }
                var path = $"{folder}/{name}.unity";
                if (paths.Contains(path)) continue;
                paths.Add(path);
                if (File.Exists(path)) r.Skipped($"Scene {name} already exists");
                else toCreate.Add(path);
            }

            if (toCreate.Count > 0)
            {
                // Creating a scene replaces the open ones, so unsaved changes must be saved (or dropped) first.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    r.Skipped("Scenes not created: you cancelled saving the open scene");
                    return paths;
                }
                var setup = EditorSceneManager.GetSceneManagerSetup();
                foreach (var path in toCreate)
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    if (EditorSceneManager.SaveScene(scene, path)) r.Done($"Created scene {Path.GetFileNameWithoutExtension(path)}");
                    else r.Error($"Couldn't save {path}");
                }

                // Reopen what was open before. Untitled scenes can't be reopened, so they are dropped.
                var saved = setup.Where(s => !string.IsNullOrEmpty(s.path) && File.Exists(s.path)).ToArray();
                if (saved.Length > 0)
                {
                    if (!saved.Any(s => s.isActive)) saved[0].isActive = true;
                    EditorSceneManager.RestoreSceneManagerSetup(saved);
                }
                else EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
            }

            if (p.addScenesToBuild) AddToBuild(paths, r);
            return paths;
        }

        public static void AddToBuild(IEnumerable<string> scenePaths, SetupReport r)
        {
            var list = EditorBuildSettings.scenes.ToList();
            int added = 0;
            foreach (var path in scenePaths)
            {
                if (!File.Exists(path) || list.Any(s => s.path == path)) continue;
                list.Add(new EditorBuildSettingsScene(path, true));
                added++;
            }
            if (added == 0) return;
            EditorBuildSettings.scenes = list.ToArray();
            r.Done($"Added {added} scene(s) to the build list");
        }

        // ================================================================ packages

        /// <summary>Ids of every package the project has now (direct and indirect).</summary>
        public static HashSet<string> InstalledPackageIds() =>
            new HashSet<string>(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(pi => pi.name));

        /// <summary>What Package Manager should add: the ticked packages that aren't installed yet.</summary>
        public static List<string> PlanPackages(SetupProfile p, ICollection<string> installed)
        {
            return p.packages
                .Where(e => e.selected && !string.IsNullOrEmpty(e.id) && !installed.Contains(e.id))
                .Select(e => e.InstallString(p.atthreeLatest))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Starts installing. It runs in the background; Unity recompiles when it finishes, so this
        /// step should come last. Returns null when there's nothing to install.
        /// </summary>
        public static AddAndRemoveRequest InstallPackages(SetupProfile p, SetupReport r)
        {
            var plan = PlanPackages(p, InstalledPackageIds());
            int ticked = p.packages.Count(e => e.selected);
            if (plan.Count == 0)
            {
                if (ticked > 0) r.Skipped("All ticked packages are already installed");
                return null;
            }
            r.Done($"Installing {plan.Count} package(s): {string.Join(", ", plan.Select(ShortName))}");
            return Client.AddAndRemove(plan.ToArray());
        }

        static string ShortName(string installString)
        {
            if (!installString.StartsWith("http")) return installString;
            var name = installString.Substring(installString.LastIndexOf('/') + 1);
            return name.Replace(".git", "");
        }
    }
}
