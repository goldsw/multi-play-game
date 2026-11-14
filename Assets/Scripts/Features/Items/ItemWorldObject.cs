using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MushroomSurvival
{
    internal enum ItemWorldRole { Bucket, Puddle, Apple, Banana }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class ItemWorldObject : NetworkBehaviour
    {
        private const float BucketFlightSeconds = 0.42f;
        private const float BucketArcTiles = 0.65f;
        private const float AppleSpeedTiles = 10f;
        private const float ProjectileRadiusTiles = 0.18f;
        private const float PuddleRadiusTiles = 0.48f;
        private const float BananaRadiusTiles = 0.40f;

        private readonly HashSet<EffectManager> slowed = new();
        private readonly List<EffectManager> endedSlow = new();
        private RaycastHit[] hits = new RaycastHit[32];
        private Collider[] overlaps = new Collider[16];
        private ItemWorld world;
        private ItemWorldRole role;
        private Vector3 origin;
        private Vector3 landing;
        private double startedAt;

        internal NetworkObject Source { get; private set; }
        internal ItemKind Item => role switch
        {
            ItemWorldRole.Apple => ItemKind.Apple,
            ItemWorldRole.Banana => ItemKind.Banana,
            _ => ItemKind.Water
        };

        internal void Initialize(ItemWorld world, ItemWorldRole role, NetworkObject source, Vector3 landing)
        {
            this.world = world;
            this.role = role;
            this.landing = landing;
            Source = source;
            origin = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                startedAt = NetworkManager.ServerTime.Time;
        }

        public override void OnNetworkDespawn()
        {
            if (world == null)
                return;

            foreach (var target in slowed)
            {
                if (target != null && target.IsSpawned)
                    target.RemoveEffect(world.Slow, NetworkObjectId);
            }
            slowed.Clear();
            world.Unregister(this);
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || world == null)
                return;

            float elapsed = (float)(NetworkManager.ServerTime.Time - startedAt);
            switch (role)
            {
                case ItemWorldRole.Apple:
                    UpdateApple(elapsed);
                    break;
                case ItemWorldRole.Bucket:
                    UpdateBucket(elapsed);
                    break;
                case ItemWorldRole.Puddle:
                    UpdatePuddle(elapsed);
                    break;
                case ItemWorldRole.Banana:
                    UpdateBanana();
                    break;
            }
        }

        private void UpdateApple(float elapsed)
        {
            float tiles = Mathf.Min(ItemWorld.AppleRangeTiles, AppleSpeedTiles * elapsed);
            Vector3 position = origin + transform.forward * (tiles * world.Map.TileSize);
            if (TrySweep(position, true, out var target))
            {
                if (target != null)
                    target.TryEnqueue(new EffectRequest(world.Stun, sourcePlayerId: SourceId));
                NetworkObject.Despawn();
                return;
            }

            transform.position = position;
            if (tiles >= ItemWorld.AppleRangeTiles)
                NetworkObject.Despawn();
        }

        private void UpdateBucket(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / BucketFlightSeconds);
            Vector3 position = Vector3.Lerp(origin, landing, progress);
            position.y += 4f * progress * (1f - progress) * BucketArcTiles * world.Map.TileSize;
            if (TrySweep(position, false, out _))
            {
                NetworkObject.Despawn();
                return;
            }

            transform.position = position;
            if (progress >= 1f)
            {
                world.LandBucket(Source, landing);
                NetworkObject.Despawn();
            }
        }

        private bool TrySweep(Vector3 position, bool hitPlayers, out EffectManager target)
        {
            var physics = gameObject.scene.GetPhysicsScene();
            var delta = position - transform.position;
            float distance = delta.magnitude;
            float radius = ProjectileRadiusTiles * world.Map.TileSize;
            target = null;
            float nearest = float.PositiveInfinity;
            int count;

            // SphereCast omits colliders intersecting its origin.
            while ((count = physics.OverlapSphere(transform.position, radius, overlaps,
                       Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) == overlaps.Length)
                Array.Resize(ref overlaps, overlaps.Length * 2);
            for (int index = 0; index < count; index++)
                ResolveHit(overlaps[index], 0f, hitPlayers, ref nearest, ref target);

            if (distance > 0f && nearest > 0f)
            {
                while ((count = physics.SphereCast(transform.position, radius, delta / distance, hits,
                           distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) == hits.Length)
                    Array.Resize(ref hits, hits.Length * 2);
                for (int index = 0; index < count; index++)
                    ResolveHit(hits[index].collider, hits[index].distance, hitPlayers, ref nearest, ref target);
            }

            return !float.IsPositiveInfinity(nearest);
        }

        private void ResolveHit(Collider collider, float distance, bool hitPlayers,
            ref float nearest, ref EffectManager target)
        {
            if (distance > nearest)
                return;

            if (world.Map.IsBorder(collider))
            {
                target = null;
                nearest = distance;
            }
            else if (hitPlayers && !collider.isTrigger && distance < nearest)
            {
                var candidate = collider.GetComponentInParent<EffectManager>();
                if (!IsLivingPlayer(candidate) || candidate.NetworkObject == Source)
                    return;

                target = candidate;
                nearest = distance;
            }
        }

        private void UpdatePuddle(float elapsed)
        {
            if (elapsed >= ItemWorld.PuddleLifetimeSeconds)
            {
                NetworkObject.Despawn();
                return;
            }

            foreach (var target in slowed)
            {
                if (!IsStandingOn(target, PuddleRadiusTiles))
                    endedSlow.Add(target);
            }
            foreach (var target in endedSlow)
            {
                slowed.Remove(target);
                if (target != null && target.IsSpawned)
                    target.RemoveEffect(world.Slow, NetworkObjectId);
            }
            endedSlow.Clear();

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !client.PlayerObject.TryGetComponent<EffectManager>(out var target) ||
                    slowed.Contains(target) || !IsStandingOn(target, PuddleRadiusTiles))
                    continue;

                if (target.TryEnqueue(new EffectRequest(world.Slow, sourcePlayerId: NetworkObjectId, notify: false)))
                    slowed.Add(target);
            }
        }

        private void UpdateBanana()
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || player == Source || !player.TryGetComponent<EffectManager>(out var target) ||
                    !IsStandingOn(target, BananaRadiusTiles))
                    continue;

                float speed = world.Slip.Amount;
                float travelTime = ItemWorld.SlipDistanceTiles * world.Map.TileSize / speed;
                var request = new EffectRequest(world.Slip, world.NextSlipDirection(), SourceId,
                    durationSeconds: travelTime + speed / (2f * MovementSettings.Acceleration));
                if (target.TryEnqueue(request))
                {
                    NetworkObject.Despawn();
                    return;
                }
            }
        }

        private bool IsStandingOn(EffectManager target, float radiusTiles)
        {
            if (!IsLivingPlayer(target) || !target.TryGetComponent<Collider>(out var collider))
                return false;

            float tile = world.Map.TileSize;
            var distance = collider.bounds.center - transform.position;
            if (new Vector2(distance.x, distance.z).sqrMagnitude > radiusTiles * radiusTiles * tile * tile ||
                !world.Map.TryGetPlayerFloor(collider, out var floor))
                return false;

            return Mathf.Abs(floor.y - transform.position.y) <= 0.15f * tile;
        }

        private bool IsLivingPlayer(EffectManager target) =>
            target != null && target.IsSpawned && target.gameObject.scene == gameObject.scene &&
            target.TryGetComponent<CharacterStateMachine>(out var state) && !state.IsEliminated;

        private ulong SourceId => Source != null ? Source.NetworkObjectId : EffectApplied.NoSourcePlayer;
    }
}
