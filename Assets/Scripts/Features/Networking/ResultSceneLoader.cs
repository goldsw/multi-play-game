using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    // Host-driven InGame -> Result transition through the NGO scene manager.
    public sealed class ResultSceneLoader : IDisposable
    {
        public const string SceneName = "Result";

        private readonly NetworkManager _network;
        private NetworkSceneManager _observedSceneManager;

        public ResultSceneLoader(NetworkManager network)
        {
            _network = network;
        }

        public Scene LoadedScene => SceneManager.GetSceneByName(SceneName);

        public bool TryLoad(out string error)
        {
            if (!_network.IsHost || _network.SceneManager == null)
            {
                error = "Only an active host can load the Result scene.";
                return false;
            }

            Observe();
            var status = _observedSceneManager.LoadScene(SceneName, LoadSceneMode.Additive);
            error = status == SceneEventProgressStatus.Started ? null : $"Result load failed ({status}).";
            return status == SceneEventProgressStatus.Started;
        }

        public async Task UnloadAsync()
        {
            Dispose();
            var scene = LoadedScene;
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null || operation.isDone)
                return;

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            operation.completed += _ => completion.TrySetResult(true);
            await completion.Task;
        }

        public void Dispose()
        {
            if (_observedSceneManager == null)
                return;

            _observedSceneManager.OnSceneEvent -= OnSceneEvent;
            _observedSceneManager = null;
        }

        private void Observe()
        {
            var sceneManager = _network.SceneManager;
            if (_observedSceneManager == sceneManager)
                return;

            Dispose();
            _observedSceneManager = sceneManager;
            if (_observedSceneManager != null)
                _observedSceneManager.OnSceneEvent += OnSceneEvent;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (_observedSceneManager == null || !_network.IsHost || sceneEvent.SceneName != SceneName ||
                sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted)
                return;

            // Every peer has the Result scene; the finished match scene is no longer needed.
            var match = SceneManager.GetSceneByName(InGameSceneLoader.SceneName);
            if (match.IsValid() && match.isLoaded)
                _observedSceneManager.UnloadScene(match);
        }
    }
}
