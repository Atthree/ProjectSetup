using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditorInternal;
using UnityEngine;

namespace Atthree.ProjectSetup
{
    /// <summary>Window > Atthree > Project Setup.</summary>
    public class ProjectSetupWindow : EditorWindow
    {
        const string MenuPath = "Window/Atthree/Project Setup";

        SetupProfile profile;
        SerializedObject so;
        ReorderableList folderList, sceneList;
        Vector2 scroll;
        HashSet<string> installed = new HashSet<string>();
        readonly SetupReport report = new SetupReport();
        AddAndRemoveRequest installRequest;

        static readonly string[] Sections = { "Project", "Folders", "Packages", "Settings", "Git & Scenes" };
        int tab;

        [MenuItem(MenuPath, priority = 2000)]
        public static ProjectSetupWindow Open()
        {
            var w = GetWindow<ProjectSetupWindow>("Project Setup");
            w.minSize = new Vector2(460, 420);
            return w;
        }

        void OnEnable()
        {
            profile = SetupProfile.LoadFromPrefs();
            BuildSerialized();
            RefreshInstalled();
            Events.registeredPackages += OnPackagesChanged;
        }

        void OnDisable()
        {
            Events.registeredPackages -= OnPackagesChanged;
            EditorApplication.update -= WatchInstall;
            if (profile != null)
            {
                profile.SaveToPrefs();
                DestroyImmediate(profile);
            }
        }

        void OnPackagesChanged(PackageRegistrationEventArgs _)
        {
            RefreshInstalled();
            Repaint();
        }

        void RefreshInstalled() => installed = SetupSteps.InstalledPackageIds();

        void BuildSerialized()
        {
            so = new SerializedObject(profile);
            folderList = MakeList("folders", "Folders (inside the root folder)");
            sceneList = MakeList("scenes", "Scenes");
        }

        ReorderableList MakeList(string property, string header)
        {
            var prop = so.FindProperty(property);
            var list = new ReorderableList(so, prop, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, header),
                elementHeight = EditorGUIUtility.singleLineHeight + 2,
            };
            list.drawElementCallback = (rect, i, active, focused) =>
            {
                rect.y += 1;
                rect.height = EditorGUIUtility.singleLineHeight;
                var el = prop.GetArrayElementAtIndex(i);
                el.stringValue = EditorGUI.TextField(rect, el.stringValue);
            };
            list.onAddCallback = l =>
            {
                prop.arraySize++;
                prop.GetArrayElementAtIndex(prop.arraySize - 1).stringValue = "";
            };
            return list;
        }

        // ================================================================ GUI

        void OnGUI()
        {
            if (profile == null) OnEnable();
            so.Update();

            DrawHeader();
            tab = GUILayout.Toolbar(tab, Sections, GUILayout.Height(24));
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawProject(); break;
                case 1: DrawFolders(); break;
                case 2: DrawPackages(); break;
                case 3: DrawSettings(); break;
                case 4: DrawGitAndScenes(); break;
            }
            EditorGUILayout.Space(8);
            DrawReport();
            EditorGUILayout.EndScrollView();

            if (so.ApplyModifiedProperties()) profile.SaveToPrefs();
            DrawFooter();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            EditorGUILayout.LabelField("Project Setup", style, GUILayout.Height(22));
            EditorGUILayout.LabelField(
                "Tick what you want, then Run Setup. Every step can run again safely. Your choices are remembered for your next project.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
        }

        void Prop(string name, string label = null)
        {
            var p = so.FindProperty(name);
            if (label == null) EditorGUILayout.PropertyField(p, true);
            else EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip), true);
        }

        void DrawProject()
        {
            Prop(nameof(SetupProfile.companyName));
            Prop(nameof(SetupProfile.productName));
            Prop(nameof(SetupProfile.setApplicationId), "Set Application Id");
            so.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(true))
            {
                if (profile.setApplicationId)
                    EditorGUILayout.TextField(" ", SetupNames.ToApplicationId(profile.companyName, profile.productName));
            }
            Prop(nameof(SetupProfile.rootNamespace));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(" ", profile.EffectiveNamespace);

            StepButton("Apply Project Info", () => SetupSteps.ApplyProjectInfo(profile, report));
        }

        void DrawFolders()
        {
            Prop(nameof(SetupProfile.rootFolder));
            so.ApplyModifiedProperties();
            EditorGUILayout.LabelField(" ", $"Folders go into {profile.RootPath}/", EditorStyles.miniLabel);
            Prop(nameof(SetupProfile.addGitkeep), "Add .gitkeep");
            EditorGUILayout.Space(4);
            folderList.DoLayoutList();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset List")) { profile.folders = new List<string>(SetupProfile.DefaultFolders); so.Update(); }
                if (GUILayout.Button("Create Folders")) RunStep(() => SetupSteps.CreateFolders(profile, report));
            }
        }

        void DrawPackages()
        {
            Prop(nameof(SetupProfile.atthreeLatest), "Atthree: Newest Version");
            so.ApplyModifiedProperties();

            DrawPackageGroup("Atthree packages (GitHub)", PackageSource.Git);
            DrawPackageGroup("Unity packages", PackageSource.Unity);

            var plan = SetupSteps.PlanPackages(profile, installed);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select None")) { foreach (var e in profile.packages) e.selected = false; Save(); }
                if (GUILayout.Button("Package Manager")) UnityEditor.PackageManager.UI.Window.Open("");
                using (new EditorGUI.DisabledScope(plan.Count == 0 || Busy))
                {
                    if (GUILayout.Button(plan.Count == 0 ? "Nothing to Install" : $"Install {plan.Count} Package(s)"))
                        RunStep(StartInstall);
                }
            }
        }

        void DrawPackageGroup(string title, PackageSource source)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var e in profile.packages.Where(p => p.source == source))
            {
                bool isInstalled = installed.Contains(e.id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(isInstalled))
                    {
                        var label = new GUIContent(e.displayName, $"{e.id}\n{e.InstallString(profile.atthreeLatest)}");
                        bool sel = EditorGUILayout.ToggleLeft(label, isInstalled || e.selected, GUILayout.Width(160));
                        if (!isInstalled && sel != e.selected) { e.selected = sel; Save(); }
                    }
                    var right = isInstalled ? "✓ installed" : e.description;
                    EditorGUILayout.LabelField(right, isInstalled ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
                }
            }
        }

        void DrawSettings()
        {
            Prop(nameof(SetupProfile.linearColorSpace));
            Prop(nameof(SetupProfile.forceTextSerialization));
            Prop(nameof(SetupProfile.visibleMetaFiles));
            Prop(nameof(SetupProfile.inputHandling));
            so.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(
                $"Input handling now: {SetupSteps.GetInputHandling()}. 'Both' is the safe choice: old Input.GetKey code and the Input System both work. Changing it needs a Unity restart.",
                MessageType.None);
            Prop(nameof(SetupProfile.showInNewProjects));
            StepButton("Apply Settings", () => SetupSteps.ApplySettings(profile, report));
        }

        void DrawGitAndScenes()
        {
            EditorGUILayout.LabelField("Git", EditorStyles.boldLabel);
            Prop(nameof(SetupProfile.writeGitignore), "Write .gitignore");
            Prop(nameof(SetupProfile.writeGitattributes), "Write .gitattributes");
            Prop(nameof(SetupProfile.useGitLfs), "Use Git LFS");
            Prop(nameof(SetupProfile.overwriteGitFiles), "Overwrite Existing");
            Prop(nameof(SetupProfile.gitInit), "Git Init");
            Prop(nameof(SetupProfile.extraIgnoreLines), "Extra .gitignore Lines");
            StepButton("Write Git Files", () =>
            {
                SetupSteps.WriteGitFiles(profile, report);
                if (profile.gitInit) SetupSteps.GitInit(report);
            });

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Starter scenes", EditorStyles.boldLabel);
            Prop(nameof(SetupProfile.createScenes));
            Prop(nameof(SetupProfile.addScenesToBuild), "Add To Build Profile");
            so.ApplyModifiedProperties();
            EditorGUILayout.LabelField(" ", $"Scenes go into {profile.ScenesFolder}/", EditorStyles.miniLabel);
            sceneList.DoLayoutList();
            StepButton("Create Scenes", () => SetupSteps.CreateScenes(profile, report));
        }

        void DrawReport()
        {
            if (Busy)
                EditorGUILayout.HelpBox("Installing packages… Unity will recompile when it's done.", MessageType.Info);
            if (report.lines.Count == 0) return;

            EditorGUILayout.LabelField("Last run", EditorStyles.boldLabel);
            foreach (var line in report.lines)
            {
                var icon = line.kind switch
                {
                    SetupReport.Kind.Done => "✓",
                    SetupReport.Kind.Skipped => "–",
                    SetupReport.Kind.Warning => "!",
                    _ => "✗",
                };
                var style = line.kind >= SetupReport.Kind.Warning ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.LabelField($"{icon}  {line.text}", style);
            }
            if (report.restartRequired && GUILayout.Button("Restart Unity Now"))
                RestartUnity();
        }

        void DrawFooter()
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Project Setup", "Reset every choice to its default?", "Reset", "Cancel"))
                    {
                        profile.ResetToDefaults();
                        BuildSerialized();
                        Save();
                    }
                }
                using (new EditorGUI.DisabledScope(Busy))
                {
                    var bg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                    if (GUILayout.Button("Run Setup (all steps)", GUILayout.Height(30))) RunAll();
                    GUI.backgroundColor = bg;
                }
            }
            EditorGUILayout.Space(4);
        }

        // ================================================================ running

        bool Busy => installRequest != null && !installRequest.IsCompleted;

        void Save()
        {
            profile.SaveToPrefs();
            so.Update();
        }

        void StepButton(string label, System.Action step)
        {
            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(Busy))
            {
                if (GUILayout.Button(label, GUILayout.Height(24))) RunStep(step);
            }
        }

        void RunStep(System.Action step)
        {
            so.ApplyModifiedProperties();
            profile.SaveToPrefs();
            report.Clear();
            step();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.LogToConsole();
            GUIUtility.ExitGUI();
        }

        void RunAll()
        {
            RunStep(() =>
            {
                SetupSteps.ApplyProjectInfo(profile, report);
                SetupSteps.ApplySettings(profile, report);
                SetupSteps.CreateFolders(profile, report);
                SetupSteps.WriteGitFiles(profile, report);
                if (profile.gitInit) SetupSteps.GitInit(report);
                if (profile.createScenes) SetupSteps.CreateScenes(profile, report);
                StartInstall(); // last: it recompiles Unity when it finishes
            });
        }

        void StartInstall()
        {
            RefreshInstalled();
            installRequest = SetupSteps.InstallPackages(profile, report);
            if (installRequest != null) EditorApplication.update += WatchInstall;
        }

        void WatchInstall()
        {
            if (installRequest == null || !installRequest.IsCompleted) return;
            EditorApplication.update -= WatchInstall;
            if (installRequest.Status == StatusCode.Success)
                report.Done("Packages installed");
            else
                report.Error("Package install failed: " + installRequest.Error?.message);
            installRequest = null;
            RefreshInstalled();
            Repaint();
        }

        static void RestartUnity()
        {
            if (!EditorUtility.DisplayDialog("Restart Unity", "Unity will save and reopen this project.", "Restart", "Later"))
                return;
            AssetDatabase.SaveAssets();
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorApplication.OpenProject(SetupPaths.ProjectRoot);
        }

        // ================================================================ first open

        [InitializeOnLoadMethod]
        static void ShowInNewProjects()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                var key = "Atthree.ProjectSetup.Shown." + Application.dataPath.GetHashCode();
                if (SessionState.GetBool(key, false) || EditorPrefs.GetBool(key, false)) return;
                SessionState.SetBool(key, true);

                var p = SetupProfile.LoadFromPrefs();
                bool show = p.showInNewProjects && !AssetDatabase.IsValidFolder(p.RootPath == "Assets" ? p.ScenesFolder : p.RootPath);
                DestroyImmediate(p);
                if (!show) return;
                EditorPrefs.SetBool(key, true);
                Open();
            };
        }
    }
}
