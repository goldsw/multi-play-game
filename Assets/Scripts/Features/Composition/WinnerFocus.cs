using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    // Local presentation of the host's winner announcement: zoom the camera to the winning character and
    // show the "우승!" banner on every peer. The host decides the winner and when the Result scene loads.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalPlayerCameraFollow), typeof(SpectatorMode), typeof(WinnerBanner))]
    public sealed class WinnerFocus : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("Camera follow distance while the winner is shown (default follow is ~18.9).")]
        private float focusDistance = 8f;
        [SerializeField] private string bannerText = "우승!";

        private LocalPlayerCameraFollow follow;
        private SpectatorMode spectator;
        private WinnerBanner banner;
        private SessionNetworkState state;
        private ulong focusedClientId;
        private bool focused;

        public bool IsFocused => focused;
        public ulong FocusedClientId => focusedClientId;
        public bool BannerVisible => banner != null && banner.IsVisible;

        private void Awake()
        {
            follow = GetComponent<LocalPlayerCameraFollow>();
            spectator = GetComponent<SpectatorMode>();
            banner = GetComponent<WinnerBanner>();
        }

        private void Update()
        {
            if (state == null)
                state = FindFirstObjectByType<SessionNetworkState>();
            var announcement = state != null ? state.Winner : default;
            if (!announcement.IsActive || NetworkManager.Singleton == null)
            {
                if (focused)
                    Release();
                return;
            }

            var winner = FindWinner(announcement.ClientId);
            if (winner == null)
                return;
            if (focused && focusedClientId == announcement.ClientId)
                return;

            focused = true;
            focusedClientId = announcement.ClientId;
            spectator.SetCelebrating(true);
            follow.SetOverrideTarget(winner.transform);
            follow.SetDistanceOverride(focusDistance);
            banner.Show(bannerText);
        }

        private NetworkObject FindWinner(ulong clientId)
        {
            foreach (var spawned in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (spawned != null && spawned.IsSpawned && spawned.IsPlayerObject && spawned.OwnerClientId == clientId &&
                    spawned.gameObject.scene == gameObject.scene)
                    return spawned;
            }
            return null;
        }

        private void Release()
        {
            focused = false;
            follow.SetOverrideTarget(null);
            follow.ClearDistanceOverride();
            spectator.SetCelebrating(false);
            banner.Hide();
        }

        private void OnDisable()
        {
            if (focused)
                Release();
        }
    }
}
