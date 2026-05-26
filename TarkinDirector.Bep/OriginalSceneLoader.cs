using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace tarkin.Director.Bep
{
    internal class OriginalSceneLoader : IDisposable
    {
        readonly string scenePath;

        readonly AssetBundle _bundle;

        public OriginalSceneLoader()
        {
            SceneManager.LoadScene(131, LoadSceneMode.Additive);

            _bundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(BepInEx.Paths.PluginPath, "lobby_bunker_lights"));
            scenePath = _bundle.GetAllScenePaths()[0];
            SceneManager.LoadScene(_bundle.GetAllScenePaths()[0], LoadSceneMode.Additive);

        }

        public void Dispose()
        {
            _bundle.Unload(false);

            SceneManager.UnloadScene(131);
            SceneManager.UnloadScene(scenePath);
        }
    }
}
