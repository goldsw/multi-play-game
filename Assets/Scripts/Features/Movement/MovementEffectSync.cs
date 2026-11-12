using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectManager))]
    public sealed class MovementEffectSync : NetworkBehaviour
    {
        private readonly NetworkVariable<MovementEffectState> state = new(
            MovementEffectState.Neutral, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private EffectManager effects;

        public MovementEffectState State =>
            IsSpawned && (IsServer || IsOwner) ? state.Value : MovementEffectState.Neutral;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            effects = GetComponent<EffectManager>();
            effects.ActiveEffectsChanged += RefreshState;
            RefreshState();
        }

        public override void OnNetworkDespawn()
        {
            if (effects != null)
            {
                effects.ActiveEffectsChanged -= RefreshState;
                effects = null;
            }
        }

        private void RefreshState()
        {
            bool stunned = false;
            float slowMultiplier = 1f;
            float boostMultiplier = 1f;
            Vector2 forcedVelocity = Vector2.zero;
            var activeEffects = effects.ActiveEffects;

            for (int index = 0; index < activeEffects.Count; index++)
            {
                var request = activeEffects[index].Request;
                if (request.Definition is not MovementEffectDefinition movement)
                {
                    continue;
                }

                switch (movement.Kind)
                {
                    case MovementEffectKind.Stun:
                        stunned = true;
                        break;
                    case MovementEffectKind.SpeedMultiplier:
                        slowMultiplier = Mathf.Min(slowMultiplier, movement.Amount);
                        boostMultiplier = Mathf.Max(boostMultiplier, movement.Amount);
                        break;
                    case MovementEffectKind.ForcedMovement:
                        forcedVelocity = request.Direction * movement.Amount;
                        break;
                }
            }

            state.Value = new MovementEffectState(stunned, slowMultiplier * boostMultiplier, forcedVelocity);
        }
    }
}
