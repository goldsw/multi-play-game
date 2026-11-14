using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectBroadcast))]
    public sealed class CameraEffectManager : NetworkBehaviour
    {
        [SerializeField] private CameraEffectDefinition[] cameraEffects;

        private EffectBroadcast broadcast;
        private CharacterStateMachine character;
        private CameraEffectDefinition active;
        private GameObject overlay;
        private Image image;
        private double endsAt;

        public void Configure(CameraEffectDefinition[] definitions) => cameraEffects = definitions;

        private void Awake()
        {
            broadcast = GetComponent<EffectBroadcast>();
            character = GetComponent<CharacterStateMachine>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                broadcast.Received += StartEffect;
        }

        public override void OnGainedOwnership()
        {
            broadcast.Received -= StartEffect;
            broadcast.Received += StartEffect;
        }

        public override void OnLostOwnership() => StopListening();
        public override void OnNetworkDespawn() => StopListening();

        private void StopListening()
        {
            broadcast.Received -= StartEffect;
            EndEffect();
        }

        private void StartEffect(EffectApplied applied)
        {
            if (!IsOwner || applied.TargetPlayerId != NetworkObjectId)
                return;

            foreach (var definition in cameraEffects)
            {
                if (definition == null || definition.EffectId != applied.EffectId)
                    continue;

                if (definition.Overlay == null)
                    return;

                active = definition;
                endsAt = applied.EndsAtServerTime;
                if (overlay == null)
                    CreateOverlay();
                image.sprite = definition.Overlay;
                overlay.SetActive(true);
                Update();
                return;
            }
        }

        private void Update()
        {
            if (active == null)
                return;

            double remaining = endsAt - broadcast.ServerTime;
            if (remaining <= 0d || character?.IsEliminated == true)
            {
                EndEffect();
                return;
            }

            var color = active.Tint;
            color.a *= active.OpacityAt(1f - Mathf.Clamp01((float)(remaining / active.DurationSeconds)));
            image.color = color;
        }

        private void EndEffect()
        {
            active = null;
            if (overlay != null)
                overlay.SetActive(false);
        }

        private void CreateOverlay()
        {
            overlay = new GameObject("Camera Effect Overlay", typeof(RectTransform), typeof(Canvas));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var panel = new GameObject("Screen Splatter", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            image = panel.GetComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
