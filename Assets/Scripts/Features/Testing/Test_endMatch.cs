using UnityEngine;
using UnityEngine.InputSystem;

namespace MushroomSurvival
{
    // Editor and Development Build only: F9 on the host ends the match as if the bomb exploded.
    [DisallowMultipleComponent]
    public sealed class Test_endMatch : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Unity.Netcode.NetworkManager network;

        public void Configure(Unity.Netcode.NetworkManager networkManager) => network = networkManager;

        private void Update()
        {
            if (network == null || !network.IsServer || Keyboard.current == null ||
                !Keyboard.current.f9Key.wasPressedThisFrame)
                return;

            var match = FindFirstObjectByType<MatchPlayerStateManager>();
            if (match != null)
                match.EndMatchByExplosion();
        }
#endif
    }
}
