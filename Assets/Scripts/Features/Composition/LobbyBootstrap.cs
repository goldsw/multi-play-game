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
            gameObject.AddComponent<LobbyConnectionGate>().Initialize(networkManager, networkState);
            ISessionConnector connector = new RelaySessionConnector();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var smokeTest = LobbySmokeRunner.TryGetRole(out var smokeHost, out var smokeRelay, out var smokeCode);
            if (smokeTest)
            {
                connector = smokeRelay
                    ? new RelaySessionConnector("smoke_" + System.Guid.NewGuid().ToString("N").Substring(0, 12))
                    : new DirectSessionConnector(networkManager);
            }
#endif
            sessionManager.Initialize(networkManager, networkState, playerSpawner, connector);
            presenter.Initialize(sessionManager, view);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (smokeTest)
                gameObject.AddComponent<LobbySmokeRunner>().Initialize(
                    sessionManager, networkManager, smokeHost, smokeCode);
#endif
        }
    }
}
