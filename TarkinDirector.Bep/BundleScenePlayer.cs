using System.Collections;
using tarkin.Director.Bep;
using UnityEngine;

namespace tarkin.Director.EFTRuntime
{
    [DefaultExecutionOrder(1000)]
    internal class BundleScenePlayer : MonoBehaviour
    {
        private AssetBundleLoader bundleLoader;
        private ProxyCameraController cameraController;

        private Coroutine currentOperation;

        void Start()
        {
            bundleLoader = new AssetBundleLoader();
            cameraController = new ProxyCameraController();
        }

        void LateUpdate()
        {
            HandleInputs();
            cameraController.ProcessLateUpdate(Time.deltaTime);
        }

        private void HandleInputs()
        {
            if (Input.GetKeyDown(Plugin.KeybindUnloadAll.Value.MainKey))
            {
                if (currentOperation == null)
                    currentOperation = StartCoroutine(UnloadAllRoutine());
                else
                    Plugin.Logger.LogWarning("Busy!");
            }

            if (Input.GetKeyDown(Plugin.KeybindPlayback.Value.MainKey))
            {
                if (currentOperation == null)
                {
                    var pathsToLoad = Plugin.GetConfiguredBundlePaths();
                    if (pathsToLoad.Count > 0)
                        currentOperation = StartCoroutine(ReloadSequenceRoutine(pathsToLoad));
                    else
                        Plugin.Logger.LogWarning("No bundles configured in settings!");
                }
                else
                {
                    Plugin.Logger.LogWarning("Busy!");
                }
            }

            if (Input.GetKeyDown(Plugin.KeybindToggleCameraOverride.Value.MainKey))
            {
                cameraController.ToggleOverride();
            }
        }

        private IEnumerator UnloadAllRoutine()
        {
            try
            {
                yield return bundleLoader.UnloadAllBundlesRoutine(
                    onBundleUnloaded: () => cameraController.ClearProxies()
                );
            }
            finally
            {
                currentOperation = null;
            }
        }

        private IEnumerator ReloadSequenceRoutine(System.Collections.Generic.List<string> pathsToLoad)
        {
            try
            {
                yield return bundleLoader.ReloadBundlesSequence(
                    targetPaths: pathsToLoad,
                    onSceneLoaded: (scene) => cameraController.AddCamerasFromScene(scene),
                    onBundleUnloaded: () => cameraController.ClearProxies()
                );
            }
            finally
            {
                currentOperation = null;
            }
        }

        void OnDestroy()
        {
            bundleLoader?.Dispose();
            cameraController?.Dispose();
        }
    }
}