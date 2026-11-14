using System;

namespace MushroomSurvival
{
    public enum CharacterAction
    {
        Idle,
        Walking,
        Casting,
        ForcedMovement,
        Stunned,
        Eliminated
    }

    [Flags]
    public enum CharacterEffects : byte
    {
        None = 0,
        Slowed = 1,
        Boosted = 2,
        ForcedMovement = 4,
        Stunned = 8,
        Invisible = 16,
        ScreenObscured = 32
    }
}
