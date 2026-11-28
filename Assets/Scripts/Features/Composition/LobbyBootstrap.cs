using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private SessionNetworkState networkState;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private LobbyPresenter presenter;
        [SerializeField] private LobbyView view;
        [SerializeField] private LobbyPreviewView preview;
        private bool openRoomOnStart = true;

        public void ConfigurePreview(LobbyPreviewView lobbyPreview) => preview = lobbyPreview;

        public void Configure(
            NetworkManager network,
            SessionNetworkState state,
            SessionManager manager,
            LobbyPresenter lobbyPresenter,
            LobbyView lobbyView)
        {
            networkManager = network;
            networkState = state;
            sessionManager = manager;
            presenter = lobbyPresenter;
            view = lobbyView;
        }

        private void Awake()
        {
            var slots = new SlotManager();
            networkState.Initialize(slots);
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
            var resultStore = networkState.GetComponent<MatchResultStore>();
            if (resultStore == null)
                throw new System.InvalidOperationException("SessionNetworkState needs a MatchResultStore. Run Tools/Mushroom Survival/Create Multiplayer Lobby.");
            sessionManager.Initialize(networkManager, networkState, new InGameSceneLoader(networkManager),
                new ResultSceneLoader(networkManager), resultStore, connector, slots);
            presenter.Initialize(sessionManager, view, preview);
            view.ConfigureCharacterSelection(networkState);
            gameObject.AddComponent<InGameCooldownHud>().Configure(networkManager, networkState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<Test_endMatch>().Configure(networkManager);
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
