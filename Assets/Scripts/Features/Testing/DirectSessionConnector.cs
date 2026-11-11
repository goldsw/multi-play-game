#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class DirectSessionConnector : ISessionConnector
    {
        private readonly NetworkManager network;

        public DirectSessionConnector(NetworkManager manager)
        {
            network = manager;
            network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 17777);
        }

        public Task<string> CreateAsync()
        {
            if (!network.StartHost())
                throw new InvalidOperationException("Local test host failed to start.");
            return Task.FromResult("LOCAL");
        }

        public async Task JoinAsync(string code)
        {
            if (!network.StartClient())
                throw new InvalidOperationException("Local test client failed to start.");
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!network.IsConnectedClient)
            {
                if (!network.IsListening || Time.realtimeSinceStartup >= deadline)
                    throw new InvalidOperationException("Local test client could not connect.");
                await Task.Delay(50);
            }
        }

        public Task LeaveAsync()
        {
            if (network.IsListening)
                network.Shutdown();
            return Task.CompletedTask;
        }
    }
}
#endif
