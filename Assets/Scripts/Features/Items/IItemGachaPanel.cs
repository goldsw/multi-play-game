using System;

namespace MushroomSurvival
{
    /// <summary>
    /// The panel above an item box. The box calls <see cref="LetGacha"/> from its open animation event with the
    /// host-decided result; the panel only plays the presentation and reports <see cref="Finished"/>.
    /// The panel never knows the box.
    /// </summary>
    public interface IItemGachaPanel
    {
        void LetGacha(ItemKind result);
        event Action<ItemKind> Finished;
    }
}
