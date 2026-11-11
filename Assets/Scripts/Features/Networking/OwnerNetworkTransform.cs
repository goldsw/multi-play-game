using Unity.Netcode.Components;

namespace MushroomSurvival
{
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
