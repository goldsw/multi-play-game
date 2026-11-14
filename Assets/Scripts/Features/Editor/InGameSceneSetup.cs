using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MushroomSurvival.Editor
{
    internal static class InGameSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/" + InGameSceneLoader.SceneName + ".unity";
        private static readonly string[] ArenaRoots =
        {
            "ArenaFloor", "NorthWall", "SouthWall", "WestWall", "EastWall"
        };

        public static void Configure(Scene lobby, NetworkObject playerPrefab)
        {
            var previous = SceneManager.GetActiveScene();
            var game = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyOpen = game.IsValid() && game.isLoaded;
            if (!alreadyOpen)
                game = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var presentation = ConfigureLobbyPresentation(lobby);
            foreach (var name in ArenaRoots)
            {
                var arenaObject = FindRoot(lobby, name);
                if (arenaObject == null)
                    continue;
                if (FindRoot(game, name) == null)
                    PlaceInScene(arenaObject, game);
                else
                    arenaObject.transform.SetParent(presentation.transform, true);
            }

            ConfigureGameSpawns(lobby, game, playerPrefab);
            ConfigureGameCamera(game);
            EditorSceneManager.SaveScene(game, ScenePath);
            if (!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath && scene.enabled))
            {
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath);
                EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            }

            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (!alreadyOpen)
                EditorSceneManager.CloseScene(game, true);
        }

        private static GameObject ConfigureLobbyPresentation(Scene lobby)
        {
            var root = FindRoot(lobby, "LobbyPresentation") ?? CreateRoot(lobby, "LobbyPresentation");
            foreach (var name in new[] { "Main Camera", "Directional Light" })
            {
                var item = FindRoot(lobby, name);
                if (item != null)
                    item.transform.SetParent(root.transform, true);
            }

            var floor = FindRoot(lobby, "ArenaFloor");
            if (floor != null && root.transform.Find("LobbyFloor") == null)
            {
                var visualFloor = Object.Instantiate(floor, floor.transform.position, floor.transform.rotation);
                visualFloor.name = "LobbyFloor";
                PlaceInScene(visualFloor, lobby, root.transform);
                foreach (var collider in visualFloor.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(collider);
            }

            var preview = lobby.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<LobbyPreviewView>())
                .Single();
            preview.ConfigurePresentation(root);
            return root;
        }

        private static void ConfigureGameSpawns(Scene lobby, Scene game, NetworkObject playerPrefab)
        {
            var root = FindRoot(game, "InGameComposition") ?? CreateRoot(game, "InGameComposition");
            var oldSpawner = lobby.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<NetworkPlayerSpawner>())
                .SingleOrDefault();
            var spawner = root.GetComponent<NetworkPlayerSpawner>();
            if (spawner == null)
                spawner = root.AddComponent<NetworkPlayerSpawner>();

            var savedPoints = new SerializedObject(oldSpawner != null ? oldSpawner : spawner).FindProperty("spawnPoints");
            var points = new Transform[LobbySettings.PlayerCount];
            for (int index = 0; index < points.Length; index++)
            {
                var point = index < savedPoints.arraySize
                    ? savedPoints.GetArrayElementAtIndex(index).objectReferenceValue as Transform
                    : null;
                if (point == null)
                {
                    point = CreateRoot(game, "Spawn " + (index + 1)).transform;
                    point.position = new Vector3(index % 2 == 0 ? -3f : 3f, 0.6f, index < 2 ? -2f : 2f);
                }
                PlaceInScene(point.gameObject, game, root.transform);
                points[index] = point;
            }
            spawner.Configure(playerPrefab, points);
            SkillSceneSetup.ConfigureScene(game, root);
            if (!root.TryGetComponent<PlayerCollisionManager>(out _))
                root.AddComponent<PlayerCollisionManager>();

            if (oldSpawner != null)
            {
                if (oldSpawner.TryGetComponent<PlayerCollisionManager>(out var oldCollisionManager))
                    Object.DestroyImmediate(oldCollisionManager);
                Object.DestroyImmediate(oldSpawner);
            }
        }

        private static void ConfigureGameCamera(Scene game)
        {
            if (FindRoot(game, "Main Camera") == null)
            {
                var cameraObject = CreateRoot(game, "Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 16f, -10f);
                camera.transform.LookAt(Vector3.zero);
                camera.orthographic = true;
                camera.orthographicSize = 10f;
                cameraObject.AddComponent<AudioListener>();
            }

            if (FindRoot(game, "Directional Light") == null)
            {
                var light = CreateRoot(game, "Directional Light");
                light.AddComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        private static GameObject CreateRoot(Scene scene, string name)
        {
            var root = new GameObject(name);
            PlaceInScene(root, scene);
            return root;
        }

        private static void PlaceInScene(GameObject item, Scene scene, Transform parent = null)
        {
            item.transform.SetParent(null, true);
            if (item.scene != scene)
                SceneManager.MoveGameObjectToScene(item, scene);
            item.transform.SetParent(parent, true);
        }
    }
}
