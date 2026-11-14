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
        private const string SkillPath = "Assets/Resources/Skills/SK004.asset";
        private const string EggPath = "Assets/Prefabs/DuckEgg.prefab";
        private const string EggMaterialPath = "Assets/Resources/Skills/DuckEgg.mat";
        private const string RangeMaterialPath = "Assets/Resources/Skills/SkillRange.mat";
        private const string EffectMaterialPath = "Assets/Resources/Skills/StatusEffectIndicator.mat";
        private const string FishSkillPath = "Assets/Resources/Skills/SK002.asset";
        private const string FishPath = "Assets/Prefabs/FishProjectile.prefab";
        private const string FishEffectPath = "Assets/Resources/Skills/FishSplash.asset";

        public static GameObject PrepareFish()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishPath);
            if (prefab == null)
            {
                var root = new GameObject("FishProjectile");
                try
                {
                    root.AddComponent<NetworkObject>();
                    root.AddComponent<NetworkTransform>();
                    root.AddComponent<FishProjectile>();
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    visual.name = "Fish";
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localScale = new Vector3(0.23f, 0.20f, 0.55f);
                    visual.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RangeMaterialPath);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, FishPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            var skill = new SerializedObject(AssetDatabase.LoadAssetAtPath<SkillDefinition>(FishSkillPath));
            skill.FindProperty("spawnPrefab").objectReferenceValue = prefab.GetComponent<FishProjectile>();
            skill.ApplyModifiedPropertiesWithoutUndo();
            return prefab;
        }

        public static GameObject PrepareEgg()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EggPath);
            if (prefab == null)
            {
                var root = new GameObject("DuckEgg");
                root.AddComponent<NetworkObject>();
                var egg = root.AddComponent<DuckEgg>();
                var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shell.name = "Shell";
                Object.DestroyImmediate(shell.GetComponent<Collider>());
                shell.transform.SetParent(root.transform, false);
                shell.transform.localPosition = Vector3.up * 0.24f;
                shell.transform.localScale = new Vector3(0.34f, 0.48f, 0.34f);
                var material = AssetDatabase.LoadAssetAtPath<Material>(EggMaterialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", new Color(1f, 0.93f, 0.73f));
                    AssetDatabase.CreateAsset(material, EggMaterialPath);
                }
                shell.GetComponent<Renderer>().sharedMaterial = material;
                egg.Configure(shell.transform);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, EggPath);
                Object.DestroyImmediate(root);
            }

            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillPath);
            var definition = new SerializedObject(skill);
            definition.FindProperty("spawnPrefab").objectReferenceValue = prefab.GetComponent<DuckEgg>();
            definition.ApplyModifiedPropertiesWithoutUndo();
            return prefab;
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

            caster.Configure(definition, AssetDatabase.LoadAssetAtPath<SkillDefinition>(FishSkillPath));
            input.Configure(actions);
            preview.Configure(definition, AssetDatabase.LoadAssetAtPath<Material>(RangeMaterialPath));
            indicator.Configure(AssetDatabase.LoadAssetAtPath<Material>(EffectMaterialPath));
            cameraEffects.Configure(new[] { AssetDatabase.LoadAssetAtPath<CameraEffectDefinition>(FishEffectPath) });
        }

        public static void ConfigureScene(Scene scene, GameObject composition)
        {
            var floor = scene.GetRootGameObjects().Single(root => root.name == "ArenaFloor").GetComponent<Collider>();
            if (!composition.TryGetComponent<EffectObjectManager>(out var manager))
                manager = composition.AddComponent<EffectObjectManager>();
            manager.Configure(floor);
        }
    }
}
