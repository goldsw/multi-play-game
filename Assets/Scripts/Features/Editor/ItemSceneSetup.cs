using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MushroomSurvival.Editor
{
    internal static class ItemSceneSetup
    {
        private const string PrefabFolder = "Assets/Prefabs/Items/";
        private const string AssetFolder = "Assets/Resources/test_Items/";
        private const string IconFolder = AssetFolder + "Icons/";
        private const string AnimationFolder = PrefabFolder + "ItemBoxAnimation/";
        private const string CatalogPath = AssetFolder + "ItemCatalog.asset";
        private const string BoxPrefabPath = PrefabFolder + "ItemBox.prefab";
        private const string BoxRootName = "ItemBoxes";

        // Placed boxes in the InGame arena (18 x 14, floor top at y = 0). Designers may move them afterwards.
        private static readonly Vector3[] BoxPositions =
        {
            new(-5.5f, 0f, 0f), new(5.5f, 0f, 0f), new(0f, 0f, -4.5f), new(0f, 0f, 4.5f)
        };

        public static GameObject[] PreparePrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();

            var catalog = PrepareCatalog();
            PrepareBox(catalog);

            var bucket = PrepareVisual<ItemWorldObject>("WaterBucket", PrimitiveType.Cylinder,
                new Vector3(0.38f, 0.22f, 0.38f), 0f, new Color(0.15f, 0.7f, 0.93f), moving: true);
            var puddle = PrepareVisual<ItemWorldObject>("WaterPuddle", PrimitiveType.Cylinder,
                new Vector3(0.96f, 0.012f, 0.96f), 0.015f, new Color(0.19f, 0.68f, 0.96f));
            var apple = PrepareVisual<ItemWorldObject>("AppleProjectile", PrimitiveType.Sphere,
                Vector3.one * 0.38f, 0f, new Color(0.92f, 0.14f, 0.2f), moving: true);
            var banana = PrepareVisual<ItemWorldObject>("BananaPeel", PrimitiveType.Sphere,
                new Vector3(0.55f, 0.17f, 0.4f), 0.08f, new Color(0.98f, 0.84f, 0.12f));
            // Item boxes are placed in the InGame scene, so they are not network prefabs.
            return new[] { bucket, puddle, apple, banana };
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
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(composition); // the removed MatchItems
            GetOrAdd<ItemWorld>(composition).Configure(map,
                LoadPrefab<ItemWorldObject>("WaterBucket"), LoadPrefab<ItemWorldObject>("WaterPuddle"),
                LoadPrefab<ItemWorldObject>("AppleProjectile"), LoadPrefab<ItemWorldObject>("BananaPeel"),
                PrepareEffect("AppleStun", 10, MovementEffectKind.Stun, 3f, 1f, EffectReapplication.Refresh),
                PrepareEffect("PuddleSlow", 11, MovementEffectKind.SpeedMultiplier, 5f, 0.5f, EffectReapplication.Independent),
                PrepareEffect("BananaSlip", 12, MovementEffectKind.Slip, ItemWorld.DefaultSlipSeconds,
                    ItemWorld.SlipSpeed, EffectReapplication.Refresh));
            PlaceBoxes(scene);
        }

        // In-scene placed NetworkObjects: NGO spawns them on the host when the InGame scene finishes loading.
        private static void PlaceBoxes(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
            var root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == BoxRootName);
            if (root == null)
            {
                root = new GameObject(BoxRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            for (int index = 0; index < BoxPositions.Length; index++)
            {
                var name = "ItemBox " + (index + 1);
                if (root.transform.Find(name) != null)
                    continue;

                var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                box.name = name;
                box.transform.SetParent(root.transform, false);
                box.transform.position = BoxPositions[index];
            }
        }

        private static ItemCatalog PrepareCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;

            Directory.CreateDirectory(IconFolder);
            var kinds = new[] { ItemKind.Water, ItemKind.Apple, ItemKind.Banana };
            var entries = new ItemCatalog.Entry[kinds.Length];
            for (int index = 0; index < kinds.Length; index++)
            {
                var path = IconFolder + kinds[index] + ".png";
                var icon = ItemIcon.Create(kinds[index], readable: true);
                File.WriteAllBytes(path, icon.texture.EncodeToPNG());
                Object.DestroyImmediate(icon.texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                entries[index] = new ItemCatalog.Entry
                {
                    kind = kinds[index],
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    weight = 1f // same odds as the old uniform RollItem
                };
            }

            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            catalog.Configure(entries);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        // Rebuilds the box prefab only when it is missing or still the old pooled box. Art replaces the clips, not this.
        private static void PrepareBox(ItemCatalog catalog)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
            if (existing != null && existing.TryGetComponent<ItemBox>(out _) &&
                existing.TryGetComponent<Animator>(out var boxAnimator) && boxAnimator.runtimeAnimatorController != null)
                return;

            var controller = PrepareBoxAnimation();
            var root = new GameObject("ItemBox", typeof(NetworkObject));
            var contact = root.AddComponent<BoxCollider>();
            contact.isTrigger = true;
            contact.center = new Vector3(0f, 0.5f, 0f);
            contact.size = new Vector3(1.3f, 1f, 1.3f);
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // events must fire even when not rendered
            var box = root.AddComponent<ItemBox>();

            var material = GetMaterial("ItemBox", new Color(0.96f, 0.62f, 0.18f));
            CreateCube("Body", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.8f, 0.5f, 0.8f), material);
            var lid = new GameObject("Lid").transform; // pivot on the back edge: the open clip rotates this
            lid.SetParent(root.transform, false);
            lid.localPosition = new Vector3(0f, 0.5f, -0.4f);
            CreateCube("Mesh", lid, new Vector3(0f, 0.06f, 0.4f), new Vector3(0.84f, 0.12f, 0.84f),
                GetMaterial("ItemBoxLid", new Color(0.8f, 0.45f, 0.1f)));

            var anchor = new GameObject("GachaPanelAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 1.8f, 0f);
            anchor.gameObject.SetActive(false);
            var panelObject = new GameObject("Panel");
            panelObject.transform.SetParent(anchor, false);
            panelObject.transform.localRotation = Quaternion.Euler(58f, 0f, 0f); // faces the angled game camera
            panelObject.transform.localScale = Vector3.one * 2f;
            var icon = panelObject.AddComponent<SpriteRenderer>();
            icon.enabled = false;
            panelObject.AddComponent<ItemGachaPanelPlaceholder>();
            var panel = new SerializedObject(panelObject.GetComponent<ItemGachaPanelPlaceholder>());
            panel.FindProperty("catalog").objectReferenceValue = catalog;
            panel.FindProperty("icon").objectReferenceValue = icon;
            panel.ApplyModifiedPropertiesWithoutUndo();

            var properties = new SerializedObject(box);
            properties.FindProperty("catalog").objectReferenceValue = catalog;
            properties.FindProperty("animator").objectReferenceValue = animator;
            properties.FindProperty("panelAnchor").objectReferenceValue = anchor;
            properties.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
            Object.DestroyImmediate(root);
        }

        // Temporary lid-flip clips. The open clip carries the Animation Event that starts the gacha.
        private static AnimatorController PrepareBoxAnimation()
        {
            Directory.CreateDirectory(AnimationFolder);
            var controllerPath = AnimationFolder + "ItemBox.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
                return existing;

            var closed = CreateClip("ItemBox_Closed", 0.1f, 0f, 0f, 0f);
            var open = CreateClip("ItemBox_Open", 0.8f, 0f, -110f, 0.5f);
            AnimationUtility.SetAnimationEvents(open, new[]
            {
                new AnimationEvent { time = 0.5f, functionName = nameof(ItemBox.OnOpenAnimationEvent) }
            });

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Open", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var closedState = machine.AddState("Closed");
            closedState.motion = closed;
            var openState = machine.AddState("Open");
            openState.motion = open;
            machine.defaultState = closedState;
            var opening = closedState.AddTransition(openState);
            opening.hasExitTime = false;
            opening.duration = 0f;
            opening.AddCondition(AnimatorConditionMode.If, 0f, "Open");
            var closing = openState.AddTransition(closedState);
            closing.hasExitTime = false;
            closing.duration = 0.25f;
            closing.AddCondition(AnimatorConditionMode.IfNot, 0f, "Open");
            return controller;
        }

        private static AnimationClip CreateClip(string name, float length, float from, float to, float rotateSeconds)
        {
            var clip = new AnimationClip { name = name };
            var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(rotateSeconds, to), new Keyframe(length, to));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Lid", typeof(Transform), "localEulerAnglesRaw.x"), curve);
            AssetDatabase.CreateAsset(clip, AnimationFolder + name + ".anim");
            return clip;
        }

        private static Transform CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube.transform;
        }

        private static Material GetMaterial(string name, Color color)
        {
            var path = AssetFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
