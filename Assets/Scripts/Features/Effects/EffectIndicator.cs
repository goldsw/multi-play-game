using UnityEngine;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EffectBroadcast))]
    public sealed class EffectIndicator : MonoBehaviour
    {
        [SerializeField] private Material material;

        private EffectBroadcast broadcast;
        private Collider body;
        private SkillRangeView view;
        private double visibleUntil;

        public void Configure(Material indicatorMaterial) => material = indicatorMaterial;

        private void Awake()
        {
            broadcast = GetComponent<EffectBroadcast>();
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
        }

        private void LateUpdate()
        {
            if (!broadcast.IsSpawned || broadcast.ServerTime >= visibleUntil)
            {
                visibleUntil = 0d;
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
