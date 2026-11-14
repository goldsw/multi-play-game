using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    public sealed class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;
        private EffectObjectManager effectObjects;
        private LobbyNetworkState lobby;

        public void InitializeSkills(EffectObjectManager manager, LobbyNetworkState state)
        {
            effectObjects = manager;
            lobby = state;
        }

        public void Configure(NetworkObject prefab, Transform[] positions)
        {
            playerPrefab = prefab;
            spawnPoints = positions;
        }

        public void SpawnPlayers(NetworkManager manager, IReadOnlyList<ulong> participants)
        {
            if (!manager.IsServer || participants.Count < LobbySettings.MinimumPlayersToStart ||
                participants.Count > LobbySettings.PlayerCount)
                throw new InvalidOperationException("The participant count is outside the lobby limits.");
            if (playerPrefab == null || spawnPoints == null || spawnPoints.Length < participants.Count)
                throw new InvalidOperationException("The player prefab and a spawn point for each participant are required.");

            var spawned = new List<NetworkObject>();
            var availablePoints = new List<Transform>(spawnPoints);
            var lobbySlots = lobby != null ? lobby.GetSlots() : Array.Empty<LobbySlotData>();
            try
            {
                for (var slot = 0; slot < participants.Count; slot++)
                {
                    var clientId = participants[slot];
                    if (manager.ConnectedClients[clientId].PlayerObject != null)
                        throw new InvalidOperationException("This player is already spawned.");
                    int pointIndex = UnityEngine.Random.Range(0, availablePoints.Count);
                    var point = availablePoints[pointIndex];
                    availablePoints.RemoveAt(pointIndex);
                    var player = Instantiate(playerPrefab, point.position, point.rotation);
                    spawned.Add(player);
                    SceneManager.MoveGameObjectToScene(player.gameObject, gameObject.scene);
                    if (player.TryGetComponent<SkillCaster>(out var caster))
                    {
                        int lobbyIndex = Array.FindIndex(lobbySlots, entry => entry.Occupied && entry.ClientId == clientId);
                        int character = lobbyIndex >= 0 ? lobbySlots[lobbyIndex].CharacterId : LobbySettings.DefaultCharacterId;
                        caster.Initialize(effectObjects, lobby, character);
                    }
                    player.SpawnAsPlayerObject(clientId, destroyWithScene: true);
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

        public bool AllPlayersReady(NetworkManager manager, IReadOnlyList<ulong> participants)
        {
            if (!manager.IsServer || participants.Count == 0)
                return false;
            foreach (var clientId in participants)
            {
                if (!manager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null ||
                    !client.PlayerObject.GetComponent<NetworkPlayerReadiness>().IsReady)
                    return false;
            }
            return true;
        }
    }
}
