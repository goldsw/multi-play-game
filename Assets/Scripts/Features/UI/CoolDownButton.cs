using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class CoolDownButton : MonoBehaviour
    {
        [SerializeField] private Image fill;

        private readonly List<(double Amount, double Time)> reductions = new();
        private Func<double> clock;
        private double updatedAt;
        private double sourceEndsAt;
        private float duration;
        private double remaining;
        private float fillSpeed = 1f;

        public double RemainingSeconds
        {
            get
            {
                Advance();
                return remaining;
            }
        }

        private double Now => clock?.Invoke() ?? Time.unscaledTimeAsDouble;

        public void Configure(Image indicator, Func<double> timeSource = null)
        {
            fill = indicator;
            clock = timeSource;
            Clear();
        }

        public void StartCooldown(float seconds)
        {
            double now = Now;
            ResetCountdown(now + seconds, seconds, now);
        }

        public void SetCooldown(double endsAt, float durationSeconds)
        {
            if (sourceEndsAt != endsAt || duration != durationSeconds)
                ResetCountdown(endsAt, durationSeconds, Now);
        }

        public void ReduceCooldown(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f)
                return;

            Advance();
            remaining = Math.Max(0d, remaining - seconds);
            if (remaining == 0d)
                reductions.Clear();
            Draw();
        }

        public void ReduceCooldownOverTime(float seconds, float overSeconds)
        {
            if (!float.IsFinite(seconds) || !float.IsFinite(overSeconds) || seconds <= 0f || overSeconds <= 0f)
                return;

            Advance();
            if (remaining > 0d)
                reductions.Add((seconds, overSeconds));
        }

        public void SetFillSpeed(float multiplier)
        {
            if (!float.IsFinite(multiplier) || multiplier < 0f)
                return;

            Advance();
            fillSpeed = multiplier;
        }

        public void Clear() => ResetCountdown(0d, 0f, Now);

        private void Update()
        {
            Advance();
            Draw();
        }

        private void ResetCountdown(double endsAt, float durationSeconds, double now)
        {
            sourceEndsAt = double.IsFinite(endsAt) ? endsAt : 0d;
            duration = float.IsFinite(durationSeconds) ? Mathf.Max(0f, durationSeconds) : 0f;
            remaining = Math.Clamp(sourceEndsAt - now, 0d, duration);
            updatedAt = now;
            fillSpeed = 1f;
            reductions.Clear();
            Draw();
        }

        private void Advance()
        {
            double now = Now;
            if (now <= updatedAt)
                return;

            double elapsed = now - updatedAt;
            updatedAt = now;
            if (remaining <= 0d)
                return;

            remaining -= elapsed * fillSpeed;
            for (int index = reductions.Count - 1; index >= 0; index--)
            {
                var (amount, time) = reductions[index];
                if (elapsed >= time)
                {
                    remaining -= amount;
                    reductions.RemoveAt(index);
                    continue;
                }

                double removed = amount * elapsed / time;
                remaining -= removed;
                reductions[index] = (amount - removed, time - elapsed);
            }

            if (remaining <= 0d)
            {
                remaining = 0d;
                reductions.Clear();
            }
        }

        private void Draw()
        {
            if (fill != null)
                fill.fillAmount = duration > 0f ? Mathf.Clamp01((float)(remaining / duration)) : 0f;
        }
    }
}
