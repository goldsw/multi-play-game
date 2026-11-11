using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        public void Configure(NetworkObject prefab, Transform[] positions)
        {
            playerPrefab = prefab;
            spawnPoints = positions;
        }

        public void SpawnPlayers(NetworkManager manager)
        {
            if (!manager.IsServer || manager.ConnectedClientsIds.Count != LobbySettings.PlayerCount)
                throw new InvalidOperationException("Four connected players are required.");
            if (playerPrefab == null || spawnPoints.Length < LobbySettings.PlayerCount)
                throw new InvalidOperationException("Player prefab and four spawn points are required.");

            var spawned = new List<NetworkObject>();
            try
            {
                for (var slot = 0; slot < manager.ConnectedClientsIds.Count; slot++)
                {
                    var clientId = manager.ConnectedClientsIds[slot];
                    if (manager.ConnectedClients[clientId].PlayerObject != null)
                        throw new InvalidOperationException("This player is already spawned.");
                    var point = spawnPoints[slot];
                    var player = Instantiate(playerPrefab, point.position, point.rotation);
                    spawned.Add(player);
                    player.SpawnAsPlayerObject(clientId);
                }
            }
            catch
            {
                foreach (var player in spawned)
                {
                    if (player.IsSpawned)
                        player.Despawn();
                    else
                        Destroy(player.gameObject);
                }
                throw;
            }
        }

        public bool AllPlayersReady(NetworkManager manager)
        {
            if (!manager.IsServer || manager.ConnectedClients.Count != LobbySettings.PlayerCount)
                return false;
            foreach (var client in manager.ConnectedClients.Values)
            {
                if (client.PlayerObject == null ||
                    !client.PlayerObject.GetComponent<NetworkPlayerReadiness>().IsReady)
                    return false;
            }
            return true;
        }
    }
}
