using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private LobbyNetworkState networkState;
        [SerializeField] private NetworkPlayerSpawner playerSpawner;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private LobbyPresenter presenter;
        [SerializeField] private LobbyView view;
        [SerializeField] private LobbyPreviewView preview;
        private bool openRoomOnStart = true;

        public void ConfigurePreview(LobbyPreviewView lobbyPreview) => preview = lobbyPreview;

        public void Configure(
            NetworkManager network,
            LobbyNetworkState state,
            NetworkPlayerSpawner spawner,
            SessionManager manager,
            LobbyPresenter lobbyPresenter,
            LobbyView lobbyView)
        {
            networkManager = network;
            networkState = state;
            playerSpawner = spawner;
            sessionManager = manager;
            presenter = lobbyPresenter;
            view = lobbyView;
        }

        private void Awake()
        {
            var slots = new SlotManager();
            networkState.Initialize(slots);
            playerSpawner.InitializeSkills(GetComponent<EffectObjectManager>(), networkState);
            gameObject.AddComponent<LobbyConnectionGate>().Initialize(networkManager, networkState, slots);
            ISessionConnector connector = new RelaySessionConnector();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var smokeTest = LobbySmokeRunner.TryGetRole(out var smokeHost, out var smokeRelay, out var smokeCode);
            if (smokeTest)
            {
                openRoomOnStart = false;
                connector = smokeRelay
                    ? new RelaySessionConnector("smoke_" + System.Guid.NewGuid().ToString("N").Substring(0, 12))
                    : new DirectSessionConnector(networkManager);
            }
#endif
            sessionManager.Initialize(networkManager, networkState, playerSpawner, connector, slots);
            presenter.Initialize(sessionManager, view, preview);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (smokeTest)
                gameObject.AddComponent<LobbySmokeRunner>().Initialize(
                    sessionManager, networkManager, smokeHost, smokeCode);
#endif
        }

        private async void Start()
        {
            if (openRoomOnStart)
                await sessionManager.HostAsync();
        }
    }
}
