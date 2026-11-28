using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace MushroomSurvival
{
    // Lives on the SessionNetworkState object in the persistent lobby scene, so the host-confirmed
    // result survives the InGame -> Result scene change on every peer.
    public sealed class MatchResultStore : NetworkBehaviour
    {
        private NetworkList<MatchResultEntry> entries;
        private readonly NetworkVariable<bool> ready = new();
        private readonly List<MatchResultEntry> snapshot = new();

        public IReadOnlyList<MatchResultEntry> Entries => snapshot;
        public bool IsReady => IsSpawned && ready.Value;

        // Raised when the result becomes ready or is cleared.
        public event Action Changed;

        private void Awake() => entries = new NetworkList<MatchResultEntry>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                entries.Clear();
                ready.Value = false;
            }
            entries.OnListChanged += OnEntriesChanged;
            ready.OnValueChanged += OnReadyChanged;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            entries.OnListChanged -= OnEntriesChanged;
            ready.OnValueChanged -= OnReadyChanged;
            snapshot.Clear();
            Changed?.Invoke();
        }

        public void Publish(IReadOnlyList<MatchResultEntry> result)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host can publish the match result.");
            ready.Value = false;
            entries.Clear();
            foreach (var entry in result)
                entries.Add(entry);
            ready.Value = true;
        }

        public void Clear()
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the host can clear the match result.");
            ready.Value = false;
            entries.Clear();
        }

        private void OnEntriesChanged(NetworkListEvent<MatchResultEntry> change)
        {
            Rebuild();
            if (ready.Value)
                Changed?.Invoke();
        }

        private void OnReadyChanged(bool previous, bool current)
        {
            Rebuild();
            Changed?.Invoke();
        }

        private void Refresh()
        {
            Rebuild();
            Changed?.Invoke();
        }

        private void Rebuild()
        {
            snapshot.Clear();
            for (int index = 0; index < entries.Count; index++)
                snapshot.Add(entries[index]);
        }
    }
}
