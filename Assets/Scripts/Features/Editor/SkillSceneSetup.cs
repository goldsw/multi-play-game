using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MushroomSurvival.Editor
{
    internal static class SkillSceneSetup
    {
        private const string SkillPath = "Assets/Resources/test_Skills/SK004.asset";
        private const string EggPath = "Assets/Prefabs/DuckEgg.prefab";
        private const string EggMaterialPath = "Assets/Resources/test_Skills/DuckEgg.mat";
        private const string RangeMaterialPath = "Assets/Resources/test_Skills/SkillRange.mat";
        private const string EffectMaterialPath = "Assets/Resources/test_Skills/StatusEffectIndicator.mat";
        private const string FishSkillPath = "Assets/Resources/test_Skills/SK002.asset";
        private const string FishPath = "Assets/Prefabs/FishProjectile.prefab";
        private const string FishEffectPath = "Assets/Resources/test_Skills/FishSplash.asset";
        private const string DogSkillPath = "Assets/Resources/test_Skills/SK001.asset";
        private const string TaggerSkillPath = "Assets/Resources/test_Skills/SK006.asset";
        private const string VisibilityEffectPath = "Assets/Resources/test_Skills/SquirrelInvisibility.asset";

        public static GameObject PrepareFish() =>
            PrepareProjectile<FishProjectile>(FishPath, FishSkillPath, new Vector3(0.23f, 0.20f, 0.55f));

        public static GameObject PrepareDog() =>
            PrepareProjectile<DogProjectile>("Assets/Prefabs/DogProjectile.prefab", DogSkillPath,
                new Vector3(0.31f, 0.31f, 0.31f));

        private static GameObject PrepareProjectile<T>(string path, string skillPath, Vector3 scale)
            where T : SkillProjectile
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = new GameObject(typeof(T).Name);
                try
                {
                    root.AddComponent<NetworkObject>();
                    root.AddComponent<NetworkTransform>();
                    root.AddComponent<T>();
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    visual.name = "Projectile Model";
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localScale = scale;
                    visual.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RangeMaterialPath);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            LinkPrefab<T>(skillPath, prefab);
            return prefab;
        }

        public static GameObject PrepareEgg() =>
            PrepareTrap<DuckEgg>(EggPath, SkillPath, EggMaterialPath, new Color(1f, 0.93f, 0.73f),
                new Vector3(0.34f, 0.48f, 0.34f));

        public static GameObject PrepareTaggerTrap() =>
            PrepareTrap<TaggerTrap>("Assets/Prefabs/TaggerTrap.prefab", TaggerSkillPath,
                "Assets/Resources/test_Skills/TaggerTrap.mat", new Color(0.45f, 0.69f, 0.82f),
                new Vector3(0.85f, 0.08f, 0.85f));

        private static GameObject PrepareTrap<T>(string path, string skillPath, string materialPath,
            Color color, Vector3 scale) where T : DuckEgg
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = new GameObject(typeof(T).Name);
                root.AddComponent<NetworkObject>();
                var egg = root.AddComponent<T>();
                var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shell.name = "Shell";
                Object.DestroyImmediate(shell.GetComponent<Collider>());
                shell.transform.SetParent(root.transform, false);
                shell.transform.localPosition = Vector3.up * (scale.y * 0.5f);
                shell.transform.localScale = scale;
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", color);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                shell.GetComponent<Renderer>().sharedMaterial = material;
                egg.Configure(shell.transform);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }

            LinkPrefab<T>(skillPath, prefab);
            return prefab;
        }

        private static void LinkPrefab<T>(string skillPath, GameObject prefab) where T : EffectObject
        {
            var definition = new SerializedObject(AssetDatabase.LoadAssetAtPath<SkillDefinition>(skillPath));
            definition.FindProperty("spawnPrefab").objectReferenceValue = prefab.GetComponent<T>();
            definition.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ConfigurePlayer(GameObject player, InputActionAsset actions)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillPath);
            if (!player.TryGetComponent<MovementEffectSync>(out _))
                player.AddComponent<MovementEffectSync>();
            if (!player.TryGetComponent<SkillCaster>(out var caster))
                caster = player.AddComponent<SkillCaster>();
            if (!player.TryGetComponent<SkillInput>(out var input))
                input = player.AddComponent<SkillInput>();
            if (!player.TryGetComponent<SkillRangePreview>(out var preview))
                preview = player.AddComponent<SkillRangePreview>();
            if (!player.TryGetComponent<EffectIndicator>(out var indicator))
                indicator = player.AddComponent<EffectIndicator>();
            if (!player.TryGetComponent<CameraEffectManager>(out var cameraEffects))
                cameraEffects = player.AddComponent<CameraEffectManager>();
            if (!player.TryGetComponent<CharacterAnimator>(out _))
                player.AddComponent<CharacterAnimator>();
            if (!player.TryGetComponent<CharacterVisibility>(out var visibility))
                visibility = player.AddComponent<CharacterVisibility>();

            var runnerSkills = new[] { FishSkillPath, SkillPath, DogSkillPath,
                "Assets/Resources/test_Skills/SK003.asset", "Assets/Resources/test_Skills/SK005.asset" };
            caster.Configure(runnerSkills.Select(AssetDatabase.LoadAssetAtPath<SkillDefinition>).ToArray(),
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(TaggerSkillPath));
            input.Configure(actions);
            preview.Configure(definition, AssetDatabase.LoadAssetAtPath<Material>(RangeMaterialPath));
            indicator.Configure(AssetDatabase.LoadAssetAtPath<Material>(EffectMaterialPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/test_Skills/DuckEggHit.wav"));
            cameraEffects.Configure(new[] { AssetDatabase.LoadAssetAtPath<CameraEffectDefinition>(FishEffectPath) });
            visibility.Configure(AssetDatabase.LoadAssetAtPath<VisibilityEffectDefinition>(VisibilityEffectPath));
        }

        public static void ConfigureScene(Scene scene, GameObject composition)
        {
            var floor = scene.GetRootGameObjects().Single(root => root.name == "ArenaFloor").GetComponent<Collider>();
            if (!composition.TryGetComponent<EffectObjectManager>(out var manager))
                manager = composition.AddComponent<EffectObjectManager>();
            manager.Configure(floor);
            if (!composition.TryGetComponent<MatchPlayerStateManager>(out _))
                composition.AddComponent<MatchPlayerStateManager>();
        }
    }
}
