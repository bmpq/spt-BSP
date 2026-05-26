using Audio.SpatialSystem;
using Comfort.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Systems.Effects;
using tarkin.Director.Bep;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace tarkin.Director.EFTRuntime
{
    internal class LoadedBundleInfo
    {
        public AssetBundle Bundle { get; }
        public List<Scene> Scenes { get; }

        public LoadedBundleInfo(AssetBundle bundle, List<Scene> scenes)
        {
            Bundle = bundle;
            Scenes = scenes;
        }
    }

    internal class AssetBundleLoader
    {
        private readonly Dictionary<string, LoadedBundleInfo> loadedAssetBundles = new Dictionary<string, LoadedBundleInfo>();

        public IEnumerator UnloadAllBundlesRoutine(Action onBundleUnloaded = null)
        {
            List<string> paths = loadedAssetBundles.Keys.ToList();
            foreach (var path in paths)
            {
                yield return UnloadBundleRoutine(path);
                onBundleUnloaded?.Invoke();
            }
        }

        public IEnumerator ReloadBundlesSequence(List<string> targetPaths, Action<Scene> onSceneLoaded, Action onBundleUnloaded)
        {
            var currentLoadedPaths = loadedAssetBundles.Keys.ToList();
            foreach (var loadedPath in currentLoadedPaths)
            {
                yield return UnloadBundleRoutine(loadedPath);
                onBundleUnloaded?.Invoke();
            }

            yield return new WaitForSecondsRealtime(0.5f);

            foreach (var path in targetPaths)
            {
                if (loadedAssetBundles.ContainsKey(path)) continue;
                yield return LoadBundleRoutine(path, onSceneLoaded);
            }
        }

        private IEnumerator UnloadBundleRoutine(string fullPath)
        {
            if (!loadedAssetBundles.TryGetValue(fullPath, out var info))
                yield break;

            Plugin.Logger.LogInfo($"Unloading '{Path.GetFileName(fullPath)}'...");

            List<AsyncOperation> unloadOperations = new List<AsyncOperation>();
            foreach (Scene scene in info.Scenes)
            {
                if (scene.isLoaded)
                {
                    unloadOperations.Add(SceneManager.UnloadSceneAsync(scene));
                }
            }

            foreach (var asyncOp in unloadOperations)
            {
                while (!asyncOp.isDone) yield return null;
            }

            info.Bundle.Unload(unloadAllLoadedObjects: false);
            loadedAssetBundles.Remove(fullPath);

            yield return null;

            UpdateDecals();

            Plugin.Logger.LogInfo($"Unloaded '{Path.GetFileName(fullPath)}'");
        }

        private IEnumerator LoadBundleRoutine(string fullPath, Action<Scene> onSceneLoaded)
        {
            if (!File.Exists(fullPath))
            {
                Plugin.Logger.LogWarning($"Bundle not found: '{Path.GetFileName(fullPath)}'");
                yield break;
            }

            Plugin.Logger.LogInfo($"Loading '{Path.GetFileName(fullPath)}'...");

            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(fullPath);
            yield return bundleRequest;

            AssetBundle assetBundle = bundleRequest.assetBundle;
            if (assetBundle == null)
            {
                Plugin.Logger.LogError($"Error loading asset bundle on {fullPath}");
                yield break;
            }

            string[] scenePaths = assetBundle.GetAllScenePaths();
            if (scenePaths.Length == 0)
            {
                loadedAssetBundles.Add(fullPath, new LoadedBundleInfo(assetBundle, new List<Scene>()));
                Plugin.Logger.LogWarning($"'{Path.GetFileName(fullPath)}' contains no scenes.");
                yield break;
            }

            List<Scene> loadedScenes = new List<Scene>();
            bool needsAudioReinit = false;

            // Load every scene found in the bundle sequentially
            foreach (string scenePath in scenePaths)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                while (!asyncOp.isDone) yield return null;

                Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                if (!loadedScene.isLoaded)
                {
                    Plugin.Logger.LogError($"Failed to load scene '{sceneName}' from bundle.");
                    continue;
                }

                loadedScenes.Add(loadedScene);

                // Notify coordinator
                onSceneLoaded?.Invoke(loadedScene);

                if (Plugin.ReplaceShaders.Value)
                    ReplaceShadersToNative(loadedScene);

                if (!needsAudioReinit && TryGetComponentInScene<SpatialAudioCrossSceneGroup>(loadedScene, out _))
                {
                    needsAudioReinit = true;
                }
            }

            var bundleInfo = new LoadedBundleInfo(assetBundle, loadedScenes);
            loadedAssetBundles.Add(fullPath, bundleInfo);

            yield return null; // Let Unity run Awakes/OnEnables for the new scenes

            UpdateDecals();

            // We default to setting the first scene in the bundle as the active one
            if (loadedScenes.Count > 0 && Plugin.SetActiveScene.Value)
                SceneManager.SetActiveScene(loadedScenes[0]);

            if (Plugin.CleanDecals.Value && Singleton<Effects>.Instantiated)
                Singleton<Effects>.Instance.ClearDecal();

            if (needsAudioReinit)
                yield return InitSpatialAudio();

            Plugin.Logger.LogInfo($"'{Path.GetFileName(fullPath)}': Loaded {loadedScenes.Count} scene(s) successfully.");
        }

        private void UpdateDecals()
        {
            if (StaticDeferredDecalRenderer.Instance != null)
            {
                try { StaticDeferredDecalRenderer.Instance.UpdateInstancesBuffers(); }
                catch { }
            }
        }

        private IEnumerator InitSpatialAudio()
        {
            if (Singleton<SpatialAudioSystem>.Instantiated)
            {
                Plugin.Logger.LogInfo("Found SpatialAudioCrossSceneGroup. Reinitializing SpatialAudioSystem...");
                yield return null;

                Task task = Singleton<SpatialAudioSystem>.Instance.Initialize(CancellationToken.None, null);
                while (!task.IsCompleted)
                    yield return null;

                Singleton<SpatialAudioSystem>.Instance.method_4();
            }
        }

        private bool TryGetComponentInScene<T>(Scene scene, out T component) where T : Component
        {
            component = default;
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                component = rootGameObject.GetComponentInChildren<T>(true);
                if (component != null) return true;
            }
            return false;
        }

        private void ReplaceShadersToNative(Scene scene)
        {
            int replacedShaderCount = 0;
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                foreach (Renderer rend in rootGameObject.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material mat in rend.sharedMaterials)
                    {
                        if (mat != null && mat.shader != null)
                        {
                            Shader nativeShader = Shader.Find(mat.shader.name);
                            if (nativeShader == null) continue;

                            if (mat.shader != nativeShader)
                            {
                                mat.shader = nativeShader;
                                replacedShaderCount++;
                            }
                        }
                    }
                }
            }
            if (replacedShaderCount > 0)
                Plugin.Logger.LogInfo($"Replaced {replacedShaderCount} shaders in scene '{scene.name}'");
        }

        public void Dispose()
        {
            List<string> loadedPaths = loadedAssetBundles.Keys.ToList();
            foreach (string fullPath in loadedPaths)
            {
                LoadedBundleInfo info = loadedAssetBundles[fullPath];
                if (info != null && info.Bundle != null)
                {
                    foreach (Scene scene in info.Scenes)
                    {
                        if (scene.isLoaded)
                            SceneManager.UnloadScene(scene); // unity docs says this is unsafe, but we have to do it wihtout Async because hot reloading might reload the plugin too soon
                    }

                    info.Bundle.Unload(unloadAllLoadedObjects: false);
                }
                Plugin.Logger.LogInfo($"Synchronously unloaded bundle '{Path.GetFileName(fullPath)}' for script reload.");
            }
            loadedAssetBundles.Clear();
        }
    }
}