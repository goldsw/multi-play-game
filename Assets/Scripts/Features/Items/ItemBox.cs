using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    /// <summary>
    /// Item box placed in the InGame scene. The host owns the timer, the gacha result and the grant; every peer only
    /// presents the replicated state. Late joiners read the same NetworkVariables, so no RPC is needed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class ItemBox : NetworkBehaviour
    {
        private const string OpenParameter = "Open";
        private const string OpenStateName = "Open";

        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform panelAnchor;
        [SerializeField, Min(0.1f)] private float closedSeconds = 10f;
        [SerializeField, Min(0f)] private float openAnimationSeconds = 0.8f;
        [SerializeField, Min(0f)] private float rollSeconds = 3f;

        // Declared result → openAt → state: a client reading state changes already holds the matching result.
        private readonly NetworkVariable<ItemKind> result = new(ItemKind.None);
        private readonly NetworkVariable<double> openAt = new(0d);
        private readonly NetworkVariable<ItemBoxState> state = new(ItemBoxState.Closed);
        private BoxCollider contact;
        private IItemGachaPanel panel;
        private SessionNetworkState session;
        private bool gachaStarted;

        public ItemBoxState State => state.Value;
        public ItemKind Result => result.Value;
        public double OpenAt => openAt.Value;
        internal bool PanelFinished { get; private set; }

        private void Awake()
        {
            contact = GetComponent<BoxCollider>();
            contact.isTrigger = true;
            if (panelAnchor != null)
                panel = panelAnchor.GetComponentInChildren<IItemGachaPanel>(true);
        }

        public override void OnNetworkSpawn()
        {
            session = FindFirstObjectByType<SessionNetworkState>();
            state.OnValueChanged += OnStateChanged;
            if (panel != null)
                panel.Finished += OnGachaFinished; // BoxEndHandler
            ApplyState(state.Value, joinedMidway: true);
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            if (panel != null)
                panel.Finished -= OnGachaFinished;
        }

        /// <summary>Animation event of the open clip. The animator lives on this object, so the event lands here.</summary>
        public void OnOpenAnimationEvent() => StartGacha();

        private void OnGachaFinished(ItemKind picked) => PanelFinished = true;

        private void StartGacha()
        {
            if (gachaStarted || state.Value == ItemBoxState.Closed)
                return;

            gachaStarted = true;
            if (panelAnchor != null)
                panelAnchor.gameObject.SetActive(true);
            panel?.LetGacha(result.Value);
        }

        private void OnStateChanged(ItemBoxState previous, ItemBoxState current) =>
            ApplyState(current, joinedMidway: false);

        private void ApplyState(ItemBoxState current, bool joinedMidway)
        {
            bool open = current != ItemBoxState.Closed;
            if (animator != null)
            {
                animator.SetBool(OpenParameter, open);
                // A peer that spawned while the box was already open skips straight to the open pose.
                if (joinedMidway && current is ItemBoxState.Rolling or ItemBoxState.Ready)
                    animator.Play(OpenStateName, 0, 1f);
            }

            if (!open)
            {
                gachaStarted = false;
                PanelFinished = false;
                if (panelAnchor != null)
                    panelAnchor.gameObject.SetActive(false);
            }
            else if (joinedMidway && current is ItemBoxState.Rolling or ItemBoxState.Ready)
            {
                StartGacha();
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || catalog == null)
                return;

            bool playing = session != null && session.Status == SessionStatus.Playing;
            if (!playing)
                return;

            double now = NetworkManager.ServerTime.Time;
            switch (state.Value)
            {
                case ItemBoxState.Closed:
                    if (openAt.Value <= 0d)
                        openAt.Value = now + closedSeconds;
                    else if (now >= openAt.Value)
                        Open();
                    break;
                case ItemBoxState.Opening:
                    if (now >= openAt.Value + openAnimationSeconds)
                        state.Value = ItemBoxState.Rolling;
                    break;
                case ItemBoxState.Rolling:
                    if (now >= openAt.Value + openAnimationSeconds + rollSeconds)
                        state.Value = ItemBoxState.Ready;
                    break;
                case ItemBoxState.Ready:
                    TryGrantToTouchingPlayer(now);
                    break;
            }
        }

        private void Open()
        {
            var picked = catalog.Roll(Random.value);
            if (picked == ItemKind.None)
            {
                openAt.Value = NetworkManager.ServerTime.Time + closedSeconds;
                return;
            }

            result.Value = picked;
            state.Value = ItemBoxState.Opening;
        }

        // Host only: collider contact happens on every peer, but only the host decides who gets the item.
        private void TryGrantToTouchingPlayer(double now)
        {
            var pickup = contact.bounds;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || player.gameObject.scene != gameObject.scene ||
                    !player.TryGetComponent<Collider>(out var body) || !pickup.Intersects(body.bounds) ||
                    !player.TryGetComponent<PlayerItemInventory>(out var inventory))
                    continue;

                if (!inventory.TryGrant(result.Value))
                    continue; // already holding an item (or eliminated): the box stays open

                result.Value = ItemKind.None;
                openAt.Value = now + closedSeconds;
                state.Value = ItemBoxState.Closed;
                return;
            }
        }
    }
}
