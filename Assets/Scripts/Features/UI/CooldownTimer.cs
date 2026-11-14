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

        public void Configure(Image indicator)
        {
            fill = indicator;
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

        public void Clear()
        {
            SetCooldown(0d, 0f);
            Render(0d);
        }
    }
}
