using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MushroomSurvival.Editor
{
    internal static class ItemSceneSetup
    {
        private const string PrefabFolder = "Assets/Prefabs/Items/";
        private const string AssetFolder = "Assets/Resources/Items/";

        public static GameObject[] PreparePrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();

            var box = PrepareVisual<ItemBox>("ItemBox", PrimitiveType.Cube,
                new Vector3(0.6f, 0.58f, 0.6f), 0.29f, new Color(0.96f, 0.62f, 0.18f));
            var spawner = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "ItemBoxSpawner.prefab");
            if (spawner == null)
            {
                var container = new GameObject("ItemBoxSpawner", typeof(NetworkObject));
                container.AddComponent<ItemBoxSpawner>().Configure(box.GetComponent<ItemBox>());
                spawner = SavePrefab(container);
            }

            var bucket = PrepareVisual<ItemWorldObject>("WaterBucket", PrimitiveType.Cylinder,
                new Vector3(0.38f, 0.22f, 0.38f), 0f, new Color(0.15f, 0.7f, 0.93f), moving: true);
            var puddle = PrepareVisual<ItemWorldObject>("WaterPuddle", PrimitiveType.Cylinder,
                new Vector3(0.96f, 0.012f, 0.96f), 0.015f, new Color(0.19f, 0.68f, 0.96f));
            var apple = PrepareVisual<ItemWorldObject>("AppleProjectile", PrimitiveType.Sphere,
                Vector3.one * 0.38f, 0f, new Color(0.92f, 0.14f, 0.2f), moving: true);
            var banana = PrepareVisual<ItemWorldObject>("BananaPeel", PrimitiveType.Sphere,
                new Vector3(0.55f, 0.17f, 0.4f), 0.08f, new Color(0.98f, 0.84f, 0.12f));
            return new[] { box, spawner, bucket, puddle, apple, banana };
        }

        public static void ConfigurePlayer(GameObject player, InputActionAsset actions)
        {
            GetOrAdd<PlayerItemInventory>(player);
            GetOrAdd<ItemInput>(player).Configure(actions);
        }

        public static void ConfigureScene(Scene scene, GameObject composition)
        {
            var roots = scene.GetRootGameObjects();
            var floor = roots.Single(root => root.name == "ArenaFloor");
            GetOrAdd<ItemSurface>(floor).Configure(ItemSurfaceKind.Ground);
            foreach (var wall in roots.Where(root => root.name is "NorthWall" or "SouthWall" or "EastWall" or "WestWall"))
                GetOrAdd<ItemSurface>(wall).Configure(ItemSurfaceKind.Border);

            var map = GetOrAdd<ItemMap>(composition);
            map.Configure(floor.GetComponent<Collider>(), 1f);
            GetOrAdd<MatchItems>(composition).Configure(map, LoadPrefab<ItemBoxSpawner>("ItemBoxSpawner"));
            GetOrAdd<ItemWorld>(composition).Configure(map,
                LoadPrefab<ItemWorldObject>("WaterBucket"), LoadPrefab<ItemWorldObject>("WaterPuddle"),
                LoadPrefab<ItemWorldObject>("AppleProjectile"), LoadPrefab<ItemWorldObject>("BananaPeel"),
                PrepareEffect("AppleStun", 10, MovementEffectKind.Stun, 3f, 1f, EffectReapplication.Refresh),
                PrepareEffect("PuddleSlow", 11, MovementEffectKind.SpeedMultiplier, 5f, 0.5f, EffectReapplication.Independent),
                PrepareEffect("BananaSlip", 12, MovementEffectKind.Slip, ItemWorld.DefaultSlipSeconds,
                    ItemWorld.SlipSpeed, EffectReapplication.Refresh));
        }

        private static GameObject PrepareVisual<T>(string name, PrimitiveType shape, Vector3 scale, float height,
            Color color, bool moving = false) where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            if (existing != null)
                return existing;

            var root = new GameObject(name, typeof(NetworkObject));
            if (moving)
            {
                var sync = root.AddComponent<NetworkTransform>();
                sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
            }
            if (typeof(T) == typeof(ItemBox))
            {
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, height, 0f);
                trigger.size = scale;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
            root.AddComponent<T>();

            var visual = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * height;
            visual.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, AssetFolder + name + ".mat");
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return SavePrefab(root);
        }

        private static MovementEffectDefinition PrepareEffect(string name, int id, MovementEffectKind kind,
            float duration, float amount, EffectReapplication reapplication)
        {
            var path = AssetFolder + name + ".asset";
            var effect = AssetDatabase.LoadAssetAtPath<MovementEffectDefinition>(path);
            if (effect != null)
                return effect;

            effect = ScriptableObject.CreateInstance<MovementEffectDefinition>();
            var properties = new SerializedObject(effect);
            properties.FindProperty("effectId").intValue = id;
            properties.FindProperty("kind").enumValueIndex = (int)kind;
            properties.FindProperty("durationSeconds").floatValue = duration;
            properties.FindProperty("amount").floatValue = amount;
            properties.FindProperty("reapplication").enumValueIndex = (int)reapplication;
            properties.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(effect, path);
            return effect;
        }

        private static GameObject SavePrefab(GameObject root)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + root.name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static T LoadPrefab<T>(string name) where T : Component =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab").GetComponent<T>();

        private static T GetOrAdd<T>(GameObject root) where T : Component =>
            root.TryGetComponent<T>(out var component) ? component : root.AddComponent<T>();
    }
}
