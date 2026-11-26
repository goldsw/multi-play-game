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

            // The podium and motions are filled in under ResultSceneRoot later.
            if (FindRoot(scene, "ResultSceneRoot") == null)
                CreateRoot(scene, "ResultSceneRoot");

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
