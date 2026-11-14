using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectBroadcast))]
    public sealed class EffectIndicator : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private AudioClip eggHitSound;

        private EffectBroadcast broadcast;
        private CharacterStateMachine character;
        private Collider body;
        private SkillRangeView view;
        private double visibleUntil;

        public void Configure(Material indicatorMaterial, AudioClip hitSound)
        {
            material = indicatorMaterial;
            eggHitSound = hitSound;
        }

        private void Awake()
        {
            broadcast = GetComponent<EffectBroadcast>();
            character = GetComponent<CharacterStateMachine>();
            body = GetComponent<Collider>();
            view = new SkillRangeView(transform, material, "Status Effect Indicator");
        }

        private void OnEnable() => broadcast.Received += Show;

        private void OnDisable()
        {
            broadcast.Received -= Show;
            visibleUntil = 0d;
            view.Hide();
        }

        private void Show(EffectApplied applied)
        {
            if (applied.EndsAtServerTime > visibleUntil)
                visibleUntil = applied.EndsAtServerTime;
            if (applied.EffectId == 1 && broadcast.IsOwner && eggHitSound != null)
                AudioSource.PlayClipAtPoint(eggHitSound, transform.position, 0.75f);
        }

        private void LateUpdate()
        {
            if (!broadcast.IsSpawned || broadcast.ServerTime >= visibleUntil ||
                (character != null && (character.IsEliminated ||
                    (character.Effects & CharacterEffects.Invisible) != 0)))
            {
                view.Hide();
                return;
            }

            var center = transform.position;
            if (body != null)
                center.y = body.bounds.min.y;
            view.ShowCircle(center + Vector3.up * 0.1f, 0.8f);
        }
    }
}
