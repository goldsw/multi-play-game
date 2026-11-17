using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalPlayerCameraFollow), typeof(SpectatorScreen))]
    public sealed class SpectatorMode : MonoBehaviour
    {
        private LocalPlayerCameraFollow follow;
        private SpectatorScreen screen;
        private CharacterStateMachine localCharacter;
        private CharacterStateMachine watchedCharacter;
        private EliminationResult elimination;
        private bool spectating;

        private void Awake()
        {
            follow = GetComponent<LocalPlayerCameraFollow>();
            screen = GetComponent<SpectatorScreen>();
        }

        private void Update()
        {
            var network = NetworkManager.Singleton;
            var player = network != null && network.IsClient ? network.LocalClient?.PlayerObject : null;
            var local = player != null && player.IsSpawned && player.gameObject.scene == gameObject.scene
                ? player.GetComponent<CharacterStateMachine>() : null;
            if (local != localCharacter || (local == null && elimination.Rank > 0))
                Bind(local);

            if (local == null || elimination.Rank == 0 || network.ServerTime.Time < elimination.SpectateAt)
                return;

            if (!spectating)
            {
                spectating = true;
                screen.Show(elimination.Rank);
            }
            if (watchedCharacter == null || !watchedCharacter.IsSpawned || watchedCharacter.IsEliminated)
                WatchSurvivor(network);
        }

        private void Bind(CharacterStateMachine character)
        {
            if (localCharacter != null)
                localCharacter.EliminatedChanged -= OnEliminatedChanged;

            localCharacter = character;
            watchedCharacter = null;
            follow.SetSpectatorTarget(null);
            screen.Hide();
            spectating = false;
            elimination = default;

            if (localCharacter != null)
            {
                localCharacter.EliminatedChanged += OnEliminatedChanged;
                OnEliminatedChanged(localCharacter.IsEliminated);
            }
        }

        private void OnEliminatedChanged(bool eliminated) =>
            elimination = eliminated ? localCharacter.Elimination : default;

        private void WatchSurvivor(NetworkManager network)
        {
            watchedCharacter = null;
            foreach (var player in network.SpawnManager.SpawnedObjectsList)
            {
                if (player == null || !player.IsSpawned || !player.IsPlayerObject ||
                    player.gameObject.scene != gameObject.scene ||
                    !player.TryGetComponent<CharacterStateMachine>(out var character) || character.IsEliminated)
                    continue;

                if (watchedCharacter == null || player.OwnerClientId < watchedCharacter.OwnerClientId)
                    watchedCharacter = character;
            }

            follow.SetSpectatorTarget(watchedCharacter != null ? watchedCharacter.transform : null);
        }

        private void OnDisable() => Bind(null);
    }
}
