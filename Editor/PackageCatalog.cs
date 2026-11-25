using System;
using System.Collections.Generic;

namespace Atthree.ProjectSetup
{
    public enum PackageSource
    {
        /// <summary>Package from GitHub (installed with a git URL).</summary>
        Git,
        /// <summary>Package from the Unity registry (installed by name).</summary>
        Unity,
    }

    /// <summary>One package the setup window can install.</summary>
    [Serializable]
    public class PackageEntry
    {
        public string id;          // com.atthree.environment / com.unity.inputsystem
        public string displayName;
        public string description;
        public PackageSource source;
        public string gitUrl;      // Git only, without the #tag
        public string version;     // Git: tag (v0.1.0). Unity: empty = the version Unity recommends
        public bool selected;

        /// <summary>
        /// What Package Manager receives: <c>https://...git#v0.1.0</c>, <c>https://...git</c> (latest) or
        /// <c>com.unity.x</c> / <c>com.unity.x@1.2.3</c>.
        /// </summary>
        public string InstallString(bool latest)
        {
            if (source == PackageSource.Git)
                return latest || string.IsNullOrEmpty(version) ? gitUrl : gitUrl + "#" + version;
            return string.IsNullOrEmpty(version) ? id : id + "@" + version;
        }

        public PackageEntry Clone() => (PackageEntry)MemberwiseClone();
    }

    /// <summary>The packages the window offers by default.</summary>
    public static class PackageCatalog
    {
        public const string GitHubUser = "Atthree";

        static PackageEntry Atthree(string repo, string id, string name, string version, string description, bool selected = false) => new PackageEntry
        {
            id = id,
            displayName = name,
            description = description,
            source = PackageSource.Git,
            gitUrl = $"https://github.com/{GitHubUser}/{repo}.git",
            version = version,
            selected = selected,
        };

        static PackageEntry Unity(string id, string name, string description, bool selected = false) => new PackageEntry
        {
            id = id,
            displayName = name,
            description = description,
            source = PackageSource.Unity,
            version = "",
            selected = selected,
        };

        public static List<PackageEntry> CreateDefault() => new List<PackageEntry>
        {
            Atthree("Environment", "com.atthree.environment", "Environment", "v0.1.0", "Sun, sky, fog, post-processing presets and a day/night cycle.", true),
            Atthree("SceneLoader", "com.atthree.scene-loader", "Scene Loader", "v0.1.0", "Fade transitions, loading screen, scene references, Scenes window.", true),
            Atthree("EventBus", "com.atthree.event-bus", "Event Bus", "v0.1.0", "Typed events and ScriptableObject event channels.", true),
            Atthree("GameFeel", "com.atthree.game-feel", "Game Feel", "v0.1.0", "Tweens, camera shake, hit stop, hit flash, floating text."),
            Atthree("AudioManager", "com.atthree.audio-manager", "Audio Manager", "v0.1.0", "Sound effects, music with crossfades, volume settings."),
            Atthree("SaveSystem", "com.atthree.save-system", "Save System", "v0.1.0", "Save slots, saveable components, JSON files."),
            Atthree("Health", "com.atthree.health", "Health", "v0.1.0", "Health, damage types, hitboxes, health bars."),
            Atthree("ActiveRagdoll", "com.atthree.active-ragdoll", "Active Ragdoll", "v0.2.0", "Physics-driven wobbly characters."),
            Atthree("VehiclePhysics", "com.atthree.vehicle-physics", "Vehicle Physics", "v0.2.0", "Raycast car physics with presets."),

            Unity("com.unity.inputsystem", "Input System", "Keyboard, mouse and gamepad input with actions.", true),
            Unity("com.unity.cinemachine", "Cinemachine", "Follow and orbit cameras without code.", true),
            Unity("com.unity.probuilder", "ProBuilder", "Build and edit level geometry inside Unity.", true),
            Unity("com.unity.ai.navigation", "AI Navigation", "NavMesh for enemies and NPCs that walk around."),
            Unity("com.unity.splines", "Splines", "Paths for roads, rails and moving objects."),
            Unity("com.unity.timeline", "Timeline", "Cutscenes and sequenced animations."),
            Unity("com.unity.addressables", "Addressables", "Load assets on demand, smaller builds."),
            Unity("com.unity.localization", "Localization", "Translate texts into other languages."),
            Unity("com.unity.recorder", "Recorder", "Record videos and GIFs of the Game view."),
            Unity("com.unity.formats.fbx", "FBX Exporter", "Export meshes to Blender and back."),
        };
    }
}
