namespace MushroomSurvival
{
    public readonly struct ActiveEffect
    {
        public EffectRequest Request { get; }
        public double ExpiresAt { get; }

        public ActiveEffect(EffectRequest request, double expiresAt)
        {
            Request = request;
            ExpiresAt = expiresAt;
        }
    }
}
