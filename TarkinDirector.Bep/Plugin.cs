using BepInEx;
using BepInEx.Configuration;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using tarkin.Director.EFTRuntime;
using SPT.Reflection.Patching;
using Comfort.Common;
using EFT;
using System;
using EFT.UI;
using tarkin.BSP.Bep;

namespace tarkin.Director.Bep
{
    [BepInPlugin("com.tarkin.bundlesceneplayer", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    internal class Plugin : BaseUnityPlugin
    {
        internal static new EFTLogger Logger;

        internal static ConfigEntry<string> BundleName;
        internal static ConfigEntry<float> CameraOverrideHandoverSpeed;
        internal static ConfigEntry<KeyboardShortcut> KeybindPlayback;
        internal static ConfigEntry<KeyboardShortcut> KeybindToggleCameraOverride;
        internal static ConfigEntry<KeyboardShortcut> KeybindUnloadAll;

        internal static ConfigEntry<string> PrewarmAssemblies;
        internal static ConfigEntry<bool> DisplayLogInGame;
        internal static ConfigEntry<bool> CleanDecals;
        internal static ConfigEntry<bool> ReplaceShaders;
        internal static ConfigEntry<bool> SetActiveScene;

        internal static ConfigEntry<bool> InfiniteAmmo;

        internal static ConfigEntry<bool> OverrideMalfunctionChance;
        internal static ConfigEntry<float> OverrideMalfunctionChanceFactor;

        private const int MAX_BUNDLE_SLOTS = 5;
        internal static List<ConfigEntry<string>> BundleSlots = new List<ConfigEntry<string>>();

        public static List<string> GetConfiguredBundlePaths()
        {
            var paths = new List<string>();
            foreach (var slot in BundleSlots)
            {
                if (!string.IsNullOrWhiteSpace(slot.Value))
                {
                    paths.Add(Path.Combine(BepInEx.Paths.PluginPath, slot.Value));
                }
            }
            return paths;
        }

        private PatchManager patchManager;
        private BundleScenePlayer bundleScenePlayer;

        private Action unregisterConsoleCommands;

        private IDisposable vidoe;

        private void Start()
        {
            InitConfiguration();

            Logger = new EFTLogger(this, () => DisplayLogInGame.Value);
            BepInEx.Logging.Logger.Sources.Add(Logger);

            Logger.LogInfo("Plugin loaded");

            bundleScenePlayer = new GameObject("Bundle Scene Player").AddComponent<BundleScenePlayer>();

            DontDestroyOnLoad(bundleScenePlayer.gameObject);

            patchManager = new PatchManager(this, autoPatch: true);
            patchManager.EnablePatches();

            unregisterConsoleCommands = ConsoleScreen.Processor.RegisterCommandGroup<ConsoleCommands>();

            //vidoe = new OriginalSceneLoader();
        }

        private void InitConfiguration()
        {
            for (int i = 0; i < MAX_BUNDLE_SLOTS; i++)
            {
                int slotNum = i + 1;
                string defaultValue = "";

                var entry = Config.Bind("Bundle Slots", $"Bundle {slotNum}", defaultValue,
                    $"Name of bundle file #{slotNum} to load.");

                BundleSlots.Add(entry);
            }

            PrewarmAssemblies = Config.Bind("General", "PrewarmAssemblies", "", 
                "Prewarm assemblies separated by a comma, without extention. " +
                "Prewarming is required when a scene contains serialized references to that assembly, since Unity doesn't load them automatically.");

            CameraOverrideHandoverSpeed = Config.Bind("General", "CameraOverrideHandoverSpeed", 2f, "");

            KeybindPlayback = Config.Bind("Keybinds", "Keybind Playback", new KeyboardShortcut(KeyCode.Insert));
            KeybindUnloadAll = Config.Bind("Keybinds", "Keybind Unload All", new KeyboardShortcut(KeyCode.Delete));
            KeybindToggleCameraOverride = Config.Bind("Keybinds", "KeybindToggleCameraOverride", new KeyboardShortcut(KeyCode.PageUp));

            DisplayLogInGame = Config.Bind("General", "DisplayLogInGame", true);
            CleanDecals = Config.Bind("General", "CleanDecalsOnLoad", true);
            ReplaceShaders = Config.Bind("General", "ReplaceShaders", false);
            SetActiveScene = Config.Bind("General", "SetActiveScene", false);

            InfiniteAmmo = Config.Bind("Gameplay", "InfiniteAmmo", false);
            OverrideMalfunctionChance = Config.Bind("Gameplay", "OverrideMalfunctionChance", false);
            OverrideMalfunctionChanceFactor = Config.Bind("Gameplay", "OverrideMalfunctionChancePercentage", 0f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));
        }

        void OnDestroy()
        {
            try
            {
                vidoe?.Dispose();
            } catch { }

            GameObject.Destroy(bundleScenePlayer.gameObject);

            patchManager.DisablePatches();

            unregisterConsoleCommands.Invoke();

            BepInEx.Logging.Logger.Sources.Remove(Logger);
            Logger = null;
        }
    }
}