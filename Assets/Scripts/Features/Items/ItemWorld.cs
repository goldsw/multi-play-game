using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival
{
    [DisallowMultipleComponent]
    public sealed class ItemWorld : MonoBehaviour
    {
        internal const float AppleRangeTiles = 8f;
        internal const float BucketRangeTiles = 2f;
        internal const float SlipDistanceTiles = 4f;
        public const float SlipSpeed = MovementSettings.Speed * 1.5f;
        public const float DefaultSlipSeconds = SlipDistanceTiles / SlipSpeed;
        internal const float PuddleLifetimeSeconds = 5f;
        private const int PlacedItemLimit = 3;

        [SerializeField] private ItemMap map;
        [SerializeField] private ItemWorldObject bucket;
        [SerializeField] private ItemWorldObject puddle;
        [SerializeField] private ItemWorldObject apple;
        [SerializeField] private ItemWorldObject banana;
        [SerializeField] private MovementEffectDefinition stun;
        [SerializeField] private MovementEffectDefinition slow;
        [SerializeField] private MovementEffectDefinition slip;

        private readonly List<ItemWorldObject> active = new();

        internal ItemMap Map => map;
        internal MovementEffectDefinition Stun => stun;
        internal MovementEffectDefinition Slow => slow;
        internal MovementEffectDefinition Slip => slip;

        public void Configure(ItemMap map, ItemWorldObject bucket, ItemWorldObject puddle,
            ItemWorldObject apple, ItemWorldObject banana, MovementEffectDefinition stun,
            MovementEffectDefinition slow, MovementEffectDefinition slip)
        {
            this.map = map;
            this.bucket = bucket;
            this.puddle = puddle;
            this.apple = apple;
            this.banana = banana;
            this.stun = stun;
            this.slow = slow;
            this.slip = slip;
        }

        public bool TryUse(NetworkObject player, ItemKind kind, Vector2 facing)
        {
            if (map == null || player == null || !player.IsSpawned ||
                player.gameObject.scene != gameObject.scene ||
                !player.TryGetComponent<CharacterStateMachine>(out var state) ||
                !state.IsServer || !state.AllowsItemUse ||
                !player.TryGetComponent<Collider>(out var body))
                return false;

            if (!float.IsFinite(facing.x) || !float.IsFinite(facing.y))
                return false;

            Vector3 direction = new Vector3(facing.x, 0f, facing.y).normalized;
            if (direction == Vector3.zero)
                direction = Vector3.forward;

            switch (kind)
            {
                case ItemKind.Water:
                    if (puddle == null || slow == null || CountActive(ItemKind.Water) >= PlacedItemLimit)
                        return false;

                    var landing = body.bounds.center + direction * (BucketRangeTiles * map.TileSize);
                    landing.y = body.bounds.min.y;
                    return Spawn(bucket, ItemWorldRole.Bucket, player, body.bounds.center, direction, landing);
                case ItemKind.Apple:
                    return stun != null &&
                           Spawn(apple, ItemWorldRole.Apple, player, body.bounds.center, direction);
                case ItemKind.Banana:
                    if (slip == null || banana == null || CountActive(ItemKind.Banana) >= PlacedItemLimit)
                        return false;

                    return !map.TryGetPlayerFloor(body, out var floor) ||
                           Spawn(banana, ItemWorldRole.Banana, player, Raised(floor), direction);
                default:
                    return false;
            }
        }

        public bool IsInUse(NetworkObject player, ItemKind kind)
        {
            foreach (var item in active)
            {
                if (item.Item == kind && item.Source == player)
                    return true;
            }

            return false;
        }

        internal void LandBucket(NetworkObject source, Vector3 landing)
        {
            if (source != null && source.IsSpawned && map.TryGetSurface(landing, out var floor))
                Spawn(puddle, ItemWorldRole.Puddle, source, Raised(floor), Vector3.forward);
        }

        internal Vector2 NextSlipDirection()
        {
            float angle = UnityEngine.Random.Range(0, 8) * (Mathf.PI / 4f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        internal void Unregister(ItemWorldObject item) => active.Remove(item);

        private Vector3 Raised(Vector3 position) => position + Vector3.up * (map.TileSize * 0.025f);

        private int CountActive(ItemKind kind)
        {
            int count = 0;
            foreach (var item in active)
            {
                if (item.Item == kind)
                    count++;
            }
            return count;
        }

        private bool Spawn(ItemWorldObject prefab, ItemWorldRole role, NetworkObject source,
            Vector3 position, Vector3 direction, Vector3 landing = default)
        {
            if (prefab == null)
                return false;

            var item = Instantiate(prefab, position, Quaternion.LookRotation(direction));
            SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene);
            item.Initialize(this, role, source, landing);
            active.Add(item);
            try
            {
                item.NetworkObject.Spawn(destroyWithScene: true);
                return true;
            }
            catch (Exception exception)
            {
                Unregister(item);
                Destroy(item.gameObject);
                Debug.LogException(exception);
                return false;
            }
        }
    }
}
