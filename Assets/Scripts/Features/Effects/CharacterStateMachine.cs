using System;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectManager))]
    public sealed class CharacterStateMachine : NetworkBehaviour
    {
        private readonly NetworkVariable<CharacterEffects> effectFlags = new();
        private readonly NetworkVariable<bool> eliminated = new();
        private readonly NetworkVariable<double> castEndsAt = new();
        private EffectManager effects;
        private Vector3 previousPosition;
        private double lastMovedAt = double.NegativeInfinity;

        public CharacterAction Action { get; private set; }
        public CharacterEffects Effects => effectFlags.Value;
        public bool IsEliminated => eliminated.Value;
        public bool StopsMovement => IsEliminated || (Effects & CharacterEffects.Stunned) != 0;
        public bool AllowsPlayerActions => IsSpawned && !IsEliminated &&
            (Effects & (CharacterEffects.Stunned | CharacterEffects.ForcedMovement)) == 0;
        public bool AllowsItemUse => AllowsPlayerActions;

        public event Action<CharacterAction, CharacterAction> ActionChanged;
        public event Action<CharacterEffects, CharacterEffects> EffectsChanged;
        public event Action<bool> EliminatedChanged;

        private void Awake() => effects = GetComponent<EffectManager>();

        public override void OnNetworkSpawn()
        {
            previousPosition = transform.position;
            lastMovedAt = double.NegativeInfinity;
            effectFlags.OnValueChanged += OnEffectsChanged;
            eliminated.OnValueChanged += OnEliminatedChanged;
            if (IsServer)
            {
                effects.ActiveEffectsChanged += RefreshEffects;
                RefreshEffects();
            }
            EffectsChanged?.Invoke(CharacterEffects.None, Effects);
            if (IsEliminated)
                EliminatedChanged?.Invoke(true);
        }

        public override void OnNetworkDespawn()
        {
            effectFlags.OnValueChanged -= OnEffectsChanged;
            eliminated.OnValueChanged -= OnEliminatedChanged;
            effects.ActiveEffectsChanged -= RefreshEffects;
            TransitionTo(CharacterAction.Idle);
        }

        internal void SetEliminated()
        {
            if (!IsSpawned || !IsServer || eliminated.Value)
                return;

            eliminated.Value = true;
            effects.ClearEffects();
        }

        internal void PlayCast()
        {
            if (IsServer)
                castEndsAt.Value = NetworkManager.ServerTime.Time + 0.25d;
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
                return;

            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            delta.y = 0f;
            TransitionTo(ResolveAction(delta));
        }

        private CharacterAction ResolveAction(Vector3 planarDelta)
        {
            if (IsEliminated)
                return CharacterAction.Eliminated;
            if ((Effects & CharacterEffects.Stunned) != 0)
                return CharacterAction.Stunned;
            if ((Effects & CharacterEffects.ForcedMovement) != 0)
                return CharacterAction.ForcedMovement;
            if (castEndsAt.Value > NetworkManager.ServerTime.Time)
                return CharacterAction.Casting;
            if (planarDelta.sqrMagnitude > 0.000001f)
                lastMovedAt = Time.unscaledTimeAsDouble;
            return Time.unscaledTimeAsDouble - lastMovedAt < 0.12d ? CharacterAction.Walking : CharacterAction.Idle;
        }

        private void TransitionTo(CharacterAction next)
        {
            if (next == Action)
                return;

            var previous = Action;
            Action = next;
            ActionChanged?.Invoke(previous, next);
        }

        private void RefreshEffects()
        {
            CharacterEffects next = CharacterEffects.None;
            foreach (var active in effects.ActiveEffects)
                next |= active.Request.Definition.CharacterFlags;
            effectFlags.Value = next;
        }

        private void OnEffectsChanged(CharacterEffects previous, CharacterEffects current) =>
            EffectsChanged?.Invoke(previous, current);

        private void OnEliminatedChanged(bool previous, bool current) => EliminatedChanged?.Invoke(current);
    }
}
