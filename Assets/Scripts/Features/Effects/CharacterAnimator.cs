using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStateMachine))]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private static readonly int ActionParameter = Animator.StringToHash("CharacterAction");
        private static readonly int EffectsParameter = Animator.StringToHash("CharacterEffects");

        [SerializeField] private Animator animator;
        private CharacterStateMachine state;

        private void Awake()
        {
            state = GetComponent<CharacterStateMachine>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            state.ActionChanged += OnActionChanged;
            state.EffectsChanged += OnEffectsChanged;
            OnActionChanged(state.Action, state.Action);
            OnEffectsChanged(state.Effects, state.Effects);
        }

        private void OnDisable()
        {
            state.ActionChanged -= OnActionChanged;
            state.EffectsChanged -= OnEffectsChanged;
        }

        private void OnActionChanged(CharacterAction previous, CharacterAction current)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetInteger(ActionParameter, (int)current);
        }

        private void OnEffectsChanged(CharacterEffects previous, CharacterEffects current)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetInteger(EffectsParameter, (int)current);
        }
    }
}
