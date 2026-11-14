using System.Linq;
using Unity.Netcode;
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

            caster.Configure(definition);
            input.Configure(actions);
            preview.Configure(definition, AssetDatabase.LoadAssetAtPath<Material>(RangeMaterialPath));
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
