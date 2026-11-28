namespace MushroomSurvival
{
    /// <summary>Closed → Opening → Rolling → Ready → Closed. Decided by the host, replicated to everyone.</summary>
    public enum ItemBoxState : byte
    {
        Closed,
        Opening,
        Rolling,
        Ready
    }
}
