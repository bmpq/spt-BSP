using Comfort.Common;
using EFT;
using EFT.CameraControl;
using System.Collections.Generic;
using tarkin.Director.Bep;
using tarkin.Director.Bep.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace tarkin.Director.EFTRuntime
{
    internal class ProxyCameraController
    {
        private List<Camera> cameraProxies = new List<Camera>();
        private float cameraOverrideFactor;
        private bool cameraOverride;
        private RenderTexture dummyRenderTexture;

        public ProxyCameraController()
        {
            dummyRenderTexture = new RenderTexture(1, 1, 16);
            dummyRenderTexture.Create();
        }

        public void ProcessLateUpdate(float deltaTime)
        {
            if (cameraOverride && cameraProxies.Count > 0 && cameraOverrideFactor < 1f)
            {
                cameraOverrideFactor += deltaTime * Plugin.CameraOverrideHandoverSpeed.Value;
                if (cameraOverrideFactor > 1f)
                    cameraOverrideFactor = 1f;
            }

            TransformGameCameraToBundleCamera(cameraOverrideFactor);
        }

        public void ToggleOverride()
        {
            cameraOverrideFactor = 0;
            cameraOverride = !cameraOverride;

            if (cameraProxies.Count == 0)
                cameraOverride = false;

            TogglePlayerCameraController(!cameraOverride);
        }

        public void ClearProxies()
        {
            cameraProxies.Clear();
            cameraOverride = false;
            TogglePlayerCameraController(true);
        }

        public void AddCamerasFromScene(Scene scene)
        {
            List<Camera> newProxies = new List<Camera>();
            foreach (GameObject rootGameObject in scene.GetRootGameObjects())
            {
                newProxies.AddRange(rootGameObject.GetComponentsInChildren<Camera>(true));
            }

            foreach (Camera cam in newProxies)
            {
                cam.cullingMask = 0;
                cam.targetTexture = dummyRenderTexture;
            }

            cameraProxies.AddRange(newProxies);
            Plugin.Logger.LogInfo($"Found {newProxies.Count} camera proxies in scene {scene.name}");
        }

        private void TransformGameCameraToBundleCamera(float t)
        {
            if (cameraProxies == null || cameraProxies.Count == 0 || t == 0)
                return;

            Camera activeProxyCamera = cameraProxies[0];
            foreach (var proxyCam in cameraProxies)
            {
                if (proxyCam.isActiveAndEnabled)
                {
                    activeProxyCamera = proxyCam;
                }
            }

            if (activeProxyCamera == null)
            {
                Plugin.Logger.LogError("No proxy cameras on the loaded scene!");
                return;
            }

            if (!CameraClass.Exist || CameraClass.Instance.Camera == null)
            {
                Plugin.Logger.LogError("No real camera exists in current raid!");
                return;
            }
            Camera realCamera = CameraClass.Instance.Camera;

            realCamera.gameObject.SetActive(true);

            realCamera.transform.position = Vector3.Lerp(realCamera.transform.position, activeProxyCamera.transform.position, t);
            realCamera.transform.rotation = Quaternion.Lerp(realCamera.transform.rotation, activeProxyCamera.transform.rotation, t);
            realCamera.fieldOfView = Mathf.Lerp(realCamera.fieldOfView, activeProxyCamera.fieldOfView, t);
            realCamera.nearClipPlane = activeProxyCamera.nearClipPlane;
            realCamera.farClipPlane = activeProxyCamera.farClipPlane;
        }

        private void TogglePlayerCameraController(bool on)
        {
            if (!Singleton<GameWorld>.Instantiated)
                return;

            if (CameraClass.Exist && CameraClass.Instance.Camera != null && CameraClass.Instance.Camera.TryGetComponent<Cinemachine.CinemachineBrain>(out var cinemachine))
                cinemachine.enabled = on;

            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player != null && player.gameObject.TryGetComponent<PlayerCameraController>(out var playerCameraController))
                playerCameraController.enabled = on;
        }

        public void Dispose()
        {
            ClearProxies();
            if (dummyRenderTexture != null)
            {
                dummyRenderTexture.Release();
                dummyRenderTexture = null;
            }
        }
    }
}