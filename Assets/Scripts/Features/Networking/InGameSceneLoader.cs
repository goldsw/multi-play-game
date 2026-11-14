using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    public sealed class InGameSceneLoader : IDisposable
    {
        public const string SceneName = "InGame";

        private readonly NetworkManager _network;
        private NetworkSceneManager _observedSceneManager;
        private AsyncOperation _localLoad;

        public event Action<SceneEvent> LoadCompleted;

        public Scene LoadedScene => SceneManager.GetSceneByName(SceneName);

        public InGameSceneLoader(NetworkManager network)
        {
            _network = network;
        }

        public void Observe()
        {
            var sceneManager = _network.SceneManager;
            if (_observedSceneManager == sceneManager)
            {
                return;
            }

            Dispose();
            _observedSceneManager = sceneManager;
            if (_observedSceneManager != null)
            {
                _observedSceneManager.OnSceneEvent += OnSceneEvent;
            }
        }

        public bool TryLoad(out string error)
        {
            if (!_network.IsHost || _network.SceneManager == null)
            {
                error = "Only an active host can load the InGame scene.";
                return false;
            }

            Observe();
            var status = _observedSceneManager.LoadScene(SceneName, LoadSceneMode.Additive);
            error = status == SceneEventProgressStatus.Started ? null : $"InGame load failed ({status}).";
            return status == SceneEventProgressStatus.Started;
        }

        public async Task UnloadAsync()
        {
            Dispose();
            await WaitForCompletionAsync(_localLoad);
            _localLoad = null;

            var scene = LoadedScene;
            if (scene.IsValid() && scene.isLoaded)
            {
                await WaitForCompletionAsync(SceneManager.UnloadSceneAsync(scene));
            }
        }

        public void Dispose()
        {
            if (_observedSceneManager == null)
            {
                return;
            }

            _observedSceneManager.OnSceneEvent -= OnSceneEvent;
            _observedSceneManager = null;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (_observedSceneManager == null || sceneEvent.SceneName != SceneName)
            {
                return;
            }

            if (sceneEvent.SceneEventType == SceneEventType.Load &&
                sceneEvent.LoadSceneMode == LoadSceneMode.Additive &&
                sceneEvent.ClientId == _network.LocalClientId)
            {
                _localLoad = sceneEvent.AsyncOperation;
            }
            else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted && _network.IsHost)
            {
                LoadCompleted?.Invoke(sceneEvent);
            }
        }

        private static Task WaitForCompletionAsync(AsyncOperation operation)
        {
            if (operation == null || operation.isDone)
            {
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            operation.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }
    }
}
