using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class CooldownTimer : MonoBehaviour
    {
        [SerializeField] private Image fill;
        private double endsAt;
        private float duration;
        private Color cooldownColor;

        public void Configure(Image indicator)
        {
            fill = indicator;
            cooldownColor = fill.color;
            Clear();
        }

        public void SetCooldown(double endTime, float durationSeconds)
        {
            endsAt = endTime;
            duration = durationSeconds;
        }

        public void Render(double now)
        {
            fill.fillAmount = duration > 0f ? Mathf.Clamp01((float)((endsAt - now) / duration)) : 0f;
        }

        public void SetSkillGauge(float fraction, bool effectActive)
        {
            fill.color = effectActive ? new Color32(50, 194, 127, 255) : cooldownColor;
            fill.fillAmount = Mathf.Clamp01(fraction);
        }

        public void Clear()
        {
            SetCooldown(0d, 0f);
            SetSkillGauge(0f, false);
        }
    }
}
