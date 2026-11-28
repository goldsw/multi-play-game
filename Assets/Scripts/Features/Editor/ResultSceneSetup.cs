using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival.Editor
{
    internal static class ResultSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/" + ResultSceneLoader.SceneName + ".unity";

        [MenuItem("Tools/Mushroom Survival/Create Result Scene")]
        public static void Configure()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen)
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var root = FindRoot(scene, "ResultSceneRoot") ?? CreateRoot(scene, "ResultSceneRoot");
            ConfigureDirector(root);

            if (FindRoot(scene, "Main Camera") == null)
            {
                var cameraObject = CreateRoot(scene, "Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 4f, -10f);
                camera.transform.LookAt(new Vector3(0f, 1f, 0f));
                cameraObject.AddComponent<AudioListener>();
            }

            if (FindRoot(scene, "Directional Light") == null)
            {
                var lightObject = CreateRoot(scene, "Directional Light");
                lightObject.AddComponent<Light>().type = LightType.Directional;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath && item.enabled))
            {
                var scenes = EditorBuildSettings.scenes.Where(item => item.path != ScenePath);
                EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            }

            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (!alreadyOpen)
                EditorSceneManager.CloseScene(scene, true);
        }

        private const string DataFolder = "Assets/Data/Result";
        private const string CatalogPath = DataFolder + "/VictoryPoseCatalog.asset";
        private const string PlaceholderPath = DataFolder + "/ResultPlaceholderModel.prefab";
        private const string CubeMaterialPath = "Assets/Resources/Models/Characters/CubePlayer.mat";

        // Steps are placeholder cubes; art replaces them and only keeps the rank slots in ResultPodium.
        private static readonly (string Name, Vector3 Center, float Height)[] Slots =
        {
            ("Rank1_Slot", new Vector3(0f, 0f, 0f), 1f),
            ("Rank2_Slot", new Vector3(-2.2f, 0f, 0f), 0.6f),
            ("Rank3_Slot", new Vector3(2.2f, 0f, 0f), 0.3f),
            ("Rank4_Slot", new Vector3(0f, 0f, -2.5f), 0f),
        };

        private static void ConfigureDirector(GameObject root)
        {
            Directory.CreateDirectory(DataFolder);
            var catalog = LoadOrCreate<VictoryPoseCatalog>(CatalogPath);
            var placeholder = LoadOrCreatePlaceholder();
            var definitions = Enum.GetValues(typeof(RunnerCharacter)).Cast<RunnerCharacter>()
                .Select(LoadOrCreateDefinition).ToArray();

            var podiumTransform = root.transform.Find("Podium");
            if (podiumTransform == null)
            {
                podiumTransform = new GameObject("Podium").transform;
                podiumTransform.SetParent(root.transform, false);
                foreach (var slot in Slots)
                {
                    var slotObject = new GameObject(slot.Name);
                    slotObject.transform.SetParent(podiumTransform, false);
                    slotObject.transform.localPosition = slot.Center + new Vector3(0f, slot.Height, 0f);
                    // Models are authored facing +Z; the camera looks along +Z from the front.
                    slotObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    if (slot.Height <= 0f)
                        continue;
                    var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    step.name = slot.Name.Replace("_Slot", "_Step");
                    step.transform.SetParent(podiumTransform, false);
                    step.transform.localScale = new Vector3(1.8f, slot.Height, 1.8f);
                    step.transform.localPosition = slot.Center + new Vector3(0f, slot.Height * 0.5f, 0f);
                }
            }

            var podium = podiumTransform.GetComponent<ResultPodium>() ?? podiumTransform.gameObject.AddComponent<ResultPodium>();
            SetFields(podium, ("rankSlots", Slots.Select(slot => (UnityEngine.Object)podiumTransform.Find(slot.Name)).ToArray()));

            var director = root.GetComponent<ResultSceneDirector>() ?? root.AddComponent<ResultSceneDirector>();
            SetFields(director, ("podium", podium), ("catalog", catalog),
                ("characters", definitions.Select(item => (UnityEngine.Object)item).ToArray()),
                ("placeholderModel", placeholder));
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // TODO: 임시 CharacterDefinition(테스트용) 에셋 생성 -> 정식 캐릭터 정의가 들어오면 교체/삭제
        private static CharacterDefinition LoadOrCreateDefinition(RunnerCharacter character)
        {
            string path = $"{DataFolder}/Character_{character}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (existing != null)
                return existing;
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            SetFields(definition, ("characterId", (int)character));
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static GameObject LoadOrCreatePlaceholder()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPath);
            if (existing != null)
                return existing;
            var holder = new GameObject("ResultPlaceholderModel");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CubeMaterialPath);
            if (material != null)
                body.GetComponent<Renderer>().sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(holder, PlaceholderPath);
            UnityEngine.Object.DestroyImmediate(holder);
            return prefab;
        }

        private static void SetFields(UnityEngine.Object target, params (string Name, object Value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach (var (name, value) in values)
            {
                var property = serialized.FindProperty(name);
                switch (value)
                {
                    case int number:
                        property.intValue = number;
                        break;
                    case UnityEngine.Object[] items:
                        property.arraySize = items.Length;
                        for (int i = 0; i < items.Length; i++)
                            property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                        break;
                    case UnityEngine.Object item:
                        property.objectReferenceValue = item;
                        break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        private static GameObject CreateRoot(Scene scene, string name)
        {
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }
    }
}
