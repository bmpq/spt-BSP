using Audio.SpatialSystem;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Systems.Effects;
using tarkin.Director.Bep;
using tarkin.Director.Bep.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace tarkin.Director.EFTRuntime
{
    [DefaultExecutionOrder(1000)]
    internal class BundleScenePlayer : MonoBehaviour
    {
        private class LoadedBundleInfo
        {
            public AssetBundle Bundle { get; }
            public Scene Scene { get; }

            public LoadedBundleInfo(AssetBundle bundle, Scene scene)
            {
                Bundle = bundle;
                Scene = scene;
            }
        }

        private readonly Dictionary<string, LoadedBundleInfo> loadedAssetBundles = new Dictionary<string, LoadedBundleInfo>();

        Coroutine operation;

        List<Camera> cameraProxies = new List<Camera>();
        float cameraOverrideFactor;
        bool cameraOverride;
        RenderTexture dummyRenderTexture;

        void Start()
        {
            dummyRenderTexture = new RenderTexture(1, 1, 16);
            dummyRenderTexture.Create();
        }

        void LateUpdate()
        {
            if (Input.GetKeyDown(Plugin.KeybindUnloadAll.Value.MainKey))
            {
                if (operation == null)
                {
                    operation = StartCoroutine(UnloadAllBundlesRoutine());
                }
                else
                {
                    Plugin.Logger.LogWarning("Busy!");
                }
            }

            if (Input.GetKeyDown(Plugin.KeybindPlayback.Value.MainKey))
            {
                if (operation == null)
                {
                    var pathsToLoad = Plugin.GetConfiguredBundlePaths();

                    if (pathsToLoad.Count > 0)
                    {
                        operation = StartCoroutine(ReloadBundlesSequence(pathsToLoad));
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("No bundles configured in settings!");
                    }
                }
                else
                {
                    Plugin.Logger.LogWarning("Busy!");
                }
            }

            if (Input.GetKeyDown(Plugin.KeybindToggleCameraOverride.Value.MainKey))
            {
                cameraOverrideFactor = 0;

                cameraOverride = !cameraOverride;
                if (cameraProxies.Count == 0)
                    cameraOverride = false;

                TogglePlayerCameraController(!cameraOverride);
            }

            if (cameraOverride && cameraProxies.Count > 0 && cameraOverrideFactor < 1f)
            {
                if (cameraOverrideFactor < 1f)
                {
                    cameraOverrideFactor += Time.deltaTime * Plugin.CameraOverrideHandoverSpeed.Value;
                    if (cameraOverrideFactor > 1f)
                        cameraOverrideFactor = 1f;
                }
            }

            TransformGameCameraToBundleCamera(cameraOverrideFactor);
        }

        void TransformGameCameraToBundleCamera(float t)
        {
            if (cameraProxies == null || cameraProxies.Count == 0)
            {
                return;
            }

            if (t == 0)
                return;

            Camera activeProxyCamera = cameraProxies[0];
            foreach (var proxyCam in cameraProxies)
            {
                if (proxyCam.isActiveAndEnabled)
                {
                    activeProxyCamera = proxyCam;
                }
            }

            Patch_EnvironmentUIRoot_SetCameraActive.CurrentEnvironmentUIRoot?.SetCameraActive(false);
            Patch_EnvironmentUIRoot_SetCameraActive.CurrentEnvironmentUIRoot?.Shading?.gameObject.SetActive(false);

            if (activeProxyCamera == null)
            {
                Plugin.Logger.LogError("No proxy cameras on the loaded scene!");
                return;
            }

            Camera realCamera = CameraClass.Instance.Camera;

            if (realCamera == null)
            {
                Plugin.Logger.LogError("No real camera exists in current raid!");
                return;
            }

            realCamera.gameObject.SetActive(true);

            realCamera.transform.position = Vector3.Lerp(
                realCamera.transform.position, 
                activeProxyCamera.transform.position, t);

            realCamera.transform.rotation = Quaternion.Lerp(
                realCamera.transform.rotation, 
                activeProxyCamera.transform.rotation, t);

            realCamera.fieldOfView = Mathf.Lerp(
                realCamera.fieldOfView, 
                activeProxyCamera.fieldOfView, t);

            realCamera.nearClipPlane = activeProxyCamera.nearClipPlane;
            realCamera.farClipPlane = activeProxyCamera.farClipPlane;
        }

        IEnumerator UnloadAllBundlesRoutine()
        {
            try
            {
                List<string> paths = loadedAssetBundles.Keys.ToList();
                foreach (var path in paths)
                {
                    yield return StartCoroutine(UnloadBundleRoutine(path));
                }
            }
            finally
            {
                operation = null;
            }
        }

        IEnumerator ReloadBundlesSequence(List<string> targetPaths)
        {
            try
            {
                var currentLoadedPaths = loadedAssetBundles.Keys.ToList();
                foreach (var loadedPath in currentLoadedPaths)
                {
                    if (!targetPaths.Contains(loadedPath) || true)
                    {
                        yield return StartCoroutine(UnloadBundleRoutine(loadedPath));
                    }
                }

                yield return new WaitForSecondsRealtime(0.5f);

                foreach (var path in targetPaths)
                {
                    if (loadedAssetBundles.ContainsKey(path)) continue;

                    yield return StartCoroutine(LoadBundleRoutine(path));
                }
            }
            finally
            {
                operation = null;
            }
        }
        
        IEnumerator UnloadBundleRoutine(string fullPath)
        {
            if (!loadedAssetBundles.TryGetValue(fullPath, out var info))
            {
                yield break;
            }

            Plugin.Logger.LogInfo($"Unloading '{Path.GetFileName(fullPath)}'...");

            cameraProxies.Clear();

            if (info.Scene.isLoaded)
            {
                AsyncOperation asyncOperation = SceneManager.UnloadSceneAsync(info.Scene);
                while (!asyncOperation.isDone)
                {
                    yield return null;
                }
            }

            info.Bundle.Unload(unloadAllLoadedObjects: false);
            loadedAssetBundles.Remove(fullPath);

            yield return null;

            if (StaticDeferredDecalRenderer.Instance != null)
            {
                try { StaticDeferredDecalRenderer.Instance.UpdateInstancesBuffers(); }
                catch { }
            }

            Resources.UnloadUnusedAssets();

            Plugin.Logger.LogInfo($"Unloaded '{Path.GetFileName(fullPath)}'");
        }

        IEnumerator LoadBundleRoutine(string fullPath)
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
                loadedAssetBundles.Add(fullPath, new LoadedBundleInfo(assetBundle, default));
                Plugin.Logger.LogWarning($"'{Path.GetFileName(fullPath)}' is not a scene bundle.");
                yield break;
            }

            string sceneName = Path.GetFileNameWithoutExtension(scenePaths[0]);
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!asyncOp.isDone)
            {
                yield return null;
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (!loadedScene.isLoaded)
            {
                Plugin.Logger.LogError($"Failed to load scene '{sceneName}' from bundle.");
                assetBundle?.Unload(true);
                yield break;
            }

            cameraProxies = FindSceneCamerasAndMakeProxies(loadedScene);
            Plugin.Logger.LogInfo($"found {cameraProxies.Count} camera proxies");

            var bundleInfo = new LoadedBundleInfo(assetBundle, loadedScene);
            loadedAssetBundles.Add(fullPath, bundleInfo);

            if (Plugin.ReplaceShaders.Value)
                ReplaceShadersToNative(loadedScene);

            yield return null; // let StaticDeferredDecal instances register themselves in OnEnable()

            if (StaticDeferredDecalRenderer.Instance != null)
            {
                try { StaticDeferredDecalRenderer.Instance.UpdateInstancesBuffers(); }
                catch { }
            }

            if (Plugin.SetActiveScene.Value)
                SceneManager.SetActiveScene(loadedScene);

            if (Plugin.CleanDecals.Value && Singleton<Effects>.Instantiated)
            {
                Singleton<Effects>.Instance.ClearDecal();
            }

            if (Singleton<SpatialAudioSystem>.Instantiated && 
                TryGetComponentInScene<SpatialAudioCrossSceneGroup>(loadedScene, out var audio))
            {
                Plugin.Logger.LogInfo($"Found '{typeof(SpatialAudioCrossSceneGroup)}' on loaded scene. Reinitializing {typeof(SpatialAudioSystem)}...");
                yield return null;

                Task task = Singleton<SpatialAudioSystem>.Instance.Initialize(CancellationToken.None, null);
                while (!task.IsCompleted)
                    yield return null;

                Singleton<SpatialAudioSystem>.Instance.method_4();
            }

            Plugin.Logger.LogInfo($"'{Path.GetFileName(fullPath)}': Scene loaded successfully.");
        }

        bool TryGetComponentInScene<T>(Scene scene, out T component) where T : Component
        {
            component = default;

            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                component = rootGameObject.GetComponentInChildren<T>(true);
                if (component != null)
                    return true;
            }

            return false;
        }

        void TogglePlayerCameraController(bool on)
        {
            Player player = Singleton<GameWorld>.Instance?.MainPlayer;

            var cinemachine = CameraClass.Instance.Camera?.GetComponent<Cinemachine.CinemachineBrain>();
            if (cinemachine != null)
                cinemachine.enabled = on;

            PlayerCameraController playerCameraController = player?.gameObject.GetComponent<PlayerCameraController>();
            if (playerCameraController != null)
            {
                playerCameraController.enabled = on;
            }
        }

        List<Camera> FindSceneCamerasAndMakeProxies(Scene scene)
        {
            List<Camera> cameraProxies = new List<Camera>();
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                cameraProxies.AddRange(rootGameObject.GetComponentsInChildren<Camera>(true));
            }

            foreach (Camera cam in cameraProxies)
            {
                cam.cullingMask = 0;
                cam.targetTexture = dummyRenderTexture;
            }

            return cameraProxies;
        }

        void ReplaceShadersToNative(Scene scene)
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

                            if (nativeShader == null)
                            {
                                Plugin.Logger.LogWarning($"'{mat.shader.name}' shader not found in runtime!");
                                continue;
                            }

                            if (mat.shader != nativeShader)
                            {
                                mat.shader = nativeShader;
                                replacedShaderCount++;
                            }
                        }
                    }
                }
            }

            Plugin.Logger.LogInfo($"replaced {replacedShaderCount} shaders");
        }

        // hot reload is unloading the plugin
        void OnDestroy()
        {
            List<string> loadedPaths = loadedAssetBundles.Keys.ToList();

            foreach (string fullPath in loadedPaths)
            {
                LoadedBundleInfo info = loadedAssetBundles[fullPath];
                if (info != null && info.Bundle != null)
                {
                    if (info.Scene.isLoaded)
                        SceneManager.UnloadScene(info.Scene); // unity docs says this is unsafe, but we have to do it wihtout Async because hot reloading might reload the plugin too soon
                    info.Bundle.Unload(unloadAllLoadedObjects: false); // let tarkov's resource management handle unreferenced assets
                }

                loadedAssetBundles.Remove(fullPath);

                Plugin.Logger.LogInfo($"Unloaded '{Path.GetFileName(fullPath)}'");
            }

            cameraProxies.Clear();

            dummyRenderTexture.Release();
        }
    }
}
