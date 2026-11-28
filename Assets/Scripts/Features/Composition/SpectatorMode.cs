using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalPlayerCameraFollow), typeof(SpectatorScreen))]
    public sealed class SpectatorMode : MonoBehaviour
    {
        private readonly List<CharacterStateMachine> survivors = new();
        private LocalPlayerCameraFollow follow;
        private SpectatorScreen screen;
        private CharacterStateMachine localCharacter;
        private CharacterStateMachine watchedCharacter;
        private EliminationResult elimination;
        private bool spectating;
        private InputAction previousAction;
        private InputAction nextAction;

        public bool IsSpectating => spectating;
        public ulong WatchedClientId => watchedCharacter != null ? watchedCharacter.OwnerClientId : ulong.MaxValue;

        private void Awake()
        {
            follow = GetComponent<LocalPlayerCameraFollow>();
            screen = GetComponent<SpectatorScreen>();
            previousAction = new InputAction("SpectatePrevious", InputActionType.Button, "<Mouse>/leftButton");
            nextAction = new InputAction("SpectateNext", InputActionType.Button, "<Mouse>/rightButton");
            previousAction.performed += _ => OnClick(-1);
            nextAction.performed += _ => OnClick(1);
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
                previousAction.Enable();
                nextAction.Enable();
            }
            if (watchedCharacter == null || !watchedCharacter.IsSpawned || watchedCharacter.IsEliminated)
            {
                CollectSurvivors(network);
                SetWatched(survivors.Count > 0 ? survivors[0] : null);
            }
        }

        /// <summary>Moves to the previous (-1) or next (+1) survivor by OwnerClientId. Returns true if the target changed.</summary>
        public bool CycleTarget(int direction)
        {
            var network = NetworkManager.Singleton;
            if (!spectating || network == null)
                return false;

            CollectSurvivors(network);
            if (survivors.Count < 2)
                return false;

            int index = survivors.IndexOf(watchedCharacter);
            index = index < 0 ? 0 : (index + (direction < 0 ? -1 : 1) + survivors.Count) % survivors.Count;
            SetWatched(survivors[index]);
            return true;
        }

        private void OnClick(int direction)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            CycleTarget(direction);
        }

        private void Bind(CharacterStateMachine character)
        {
            if (localCharacter != null)
                localCharacter.EliminatedChanged -= OnEliminatedChanged;

            localCharacter = character;
            watchedCharacter = null;
            follow.SetSpectatorTarget(null);
            screen.Hide();
            previousAction.Disable();
            nextAction.Disable();
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

        private void CollectSurvivors(NetworkManager network)
        {
            survivors.Clear();
            foreach (var player in network.SpawnManager.SpawnedObjectsList)
            {
                if (player == null || !player.IsSpawned || !player.IsPlayerObject ||
                    player.gameObject.scene != gameObject.scene ||
                    !player.TryGetComponent<CharacterStateMachine>(out var character) || character.IsEliminated)
                    continue;
                survivors.Add(character);
            }
            survivors.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        }

        private void SetWatched(CharacterStateMachine character)
        {
            watchedCharacter = character;
            follow.SetSpectatorTarget(character != null ? character.transform : null);
            screen.SetWatching(character != null ? $"관전 중: 플레이어 {character.OwnerClientId + 1}" : string.Empty);
        }

        private void OnDestroy()
        {
            previousAction.Dispose();
            nextAction.Dispose();
        }

        private void OnDisable() => Bind(null);
    }
}
