using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MushroomSurvival.Editor
{
    public static class MultiplayerSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/MultiplayerLobby.unity";
        private const string PrefabPath = "Assets/Prefabs/CubePlayer.prefab";
        private const string CubeMaterialPath = "Assets/Resources/Models/Characters/CubePlayer.mat";
        private const string ArenaMaterialPath = "Assets/Resources/Models/Maps/Arena.mat";

        [MenuItem("Tools/Mushroom Survival/Create Multiplayer Lobby")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Resources/Models/Characters");
            Directory.CreateDirectory("Assets/Resources/Models/Maps");
            AssetDatabase.Refresh();
            var eggPrefab = SkillSceneSetup.PrepareEgg();
            if (File.Exists(ScenePath))
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    ConfigurePlayerMovement(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                var cachedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                cachedPrefab.GetComponent<Renderer>().sharedMaterial =
                    GetMaterial(CubeMaterialPath, new Color(0.15f, 0.6f, 0.9f));
                PrefabUtility.SavePrefabAsset(cachedPrefab);
                var cachedRegistry = GetPrefabRegistry(cachedPrefab, eggPrefab);
                var loadedScene = SceneManager.GetSceneByPath(ScenePath);
                var wasLoaded = loadedScene.IsValid() && loadedScene.isLoaded;
                if (!wasLoaded)
                    loadedScene = EditorSceneManager.OpenScene(
                        ScenePath, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var sceneNetwork = loadedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<NetworkManager>()).Single();
                foreach (var root in loadedScene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<Renderer>(out var renderer))
                        renderer.sharedMaterial = GetMaterial(ArenaMaterialPath, new Color(0.55f, 0.6f, 0.55f));
                }
                sceneNetwork.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                sceneNetwork.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(cachedRegistry);
                ConfigureLobbyPreview(loadedScene);
                ConfigurePlayerCollisions(loadedScene, sceneNetwork);
                SkillSceneSetup.ConfigureScene(loadedScene);
                EditorUtility.SetDirty(sceneNetwork);
                EditorSceneManager.SaveScene(loadedScene);
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(loadedScene, true);
                AssetDatabase.SaveAssets();
                Debug.Log("MUSHROOM_SCENE_READY " + ScenePath);
                return;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "CubePlayer";
            cube.GetComponent<Renderer>().sharedMaterial =
                GetMaterial(CubeMaterialPath, new Color(0.15f, 0.6f, 0.9f));
            cube.AddComponent<NetworkObject>();
            cube.AddComponent<OwnerNetworkTransform>();
            var body = cube.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            cube.AddComponent<NetworkRigidbody>();
            cube.AddComponent<NetworkPlayerReadiness>();
            ConfigurePlayerMovement(cube);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(cube, PrefabPath);
            Object.DestroyImmediate(cube);
            var prefabs = GetPrefabRegistry(playerPrefab, eggPrefab);

            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ArenaFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(18f, 1f, 14f);
            floor.GetComponent<Renderer>().sharedMaterial =
                GetMaterial(ArenaMaterialPath, new Color(0.55f, 0.6f, 0.55f));

            CreateWall("NorthWall", new Vector3(0f, 1f, 7f), new Vector3(18f, 2f, 0.5f));
            CreateWall("SouthWall", new Vector3(0f, 1f, -7f), new Vector3(18f, 2f, 0.5f));
            CreateWall("WestWall", new Vector3(-9f, 1f, 0f), new Vector3(0.5f, 2f, 14f));
            CreateWall("EastWall", new Vector3(9f, 1f, 0f), new Vector3(0.5f, 2f, 14f));

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 16f, -10f);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            lightObject.AddComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);

            var networkObject = new GameObject("NetworkManager");
            var network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig.NetworkTransport = transport;
            network.NetworkConfig.ConnectionApproval = true;
            network.NetworkConfig.TickRate = 30;
            network.NetworkConfig.PlayerPrefab = null;
            network.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);

            var stateObject = new GameObject("LobbyNetworkState");
            stateObject.AddComponent<NetworkObject>();
            var state = stateObject.AddComponent<LobbyNetworkState>();

            var roots = new GameObject("LobbyComposition");
            var manager = roots.AddComponent<SessionManager>();
            var spawner = roots.AddComponent<NetworkPlayerSpawner>();
            var view = roots.AddComponent<LobbyView>();
            var presenter = roots.AddComponent<LobbyPresenter>();
            var positions = new[]
            {
                new Vector3(-3f, 0.6f, -2f),
                new Vector3(3f, 0.6f, -2f),
                new Vector3(-3f, 0.6f, 2f),
                new Vector3(3f, 0.6f, 2f)
            };
            var points = positions.Select((position, index) =>
            {
                var point = new GameObject("Spawn " + (index + 1)).transform;
                point.position = position;
                point.SetParent(roots.transform);
                return point;
            }).ToArray();
            spawner.Configure(playerPrefab.GetComponent<NetworkObject>(), points);
            roots.AddComponent<LobbyBootstrap>().Configure(network, state, spawner, manager, presenter, view);
            ConfigureLobbyPreview(scene);
            ConfigurePlayerCollisions(scene, network);
            SkillSceneSetup.ConfigureScene(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var existingScenes = EditorBuildSettings.scenes.Where(item => item.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(existingScenes).ToArray();
            AssetDatabase.SaveAssets();
            if (previousScene.IsValid())
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("MUSHROOM_SCENE_CREATED " + ScenePath);
        }

        public static void BuildDevelopmentPlayer()
        {
            CreateScene();
            var report = BuildPipeline.BuildPlayer(
                new[] { ScenePath },
                "Builds/MultiplayerSmoke/MushroomSurvival.exe",
                BuildTarget.StandaloneWindows64,
                BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.InvalidOperationException("Multiplayer development build failed.");
            Debug.Log("MUSHROOM_BUILD_SUCCEEDED");
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial =
                GetMaterial(ArenaMaterialPath, new Color(0.55f, 0.6f, 0.55f));
        }

        private static void ConfigurePlayerMovement(GameObject player)
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (input == null)
                throw new System.InvalidOperationException("The player input actions asset is missing.");
            var movement = player.GetComponent<RigidbodyMovement>();
            if (movement == null)
                movement = player.AddComponent<RigidbodyMovement>();
            movement.Configure(input);
            if (!player.TryGetComponent<PlayerPositionCorrection>(out _))
                player.AddComponent<PlayerPositionCorrection>();
            SkillSceneSetup.ConfigurePlayer(player, input);
        }

        private static void ConfigurePlayerCollisions(Scene scene, NetworkManager network)
        {
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots.Select(root => root.GetComponent<LobbyBootstrap>())
                .Single(component => component != null);
            var lobby = roots.Select(root => root.GetComponent<LobbyNetworkState>())
                .Single(component => component != null);
            var manager = bootstrap.GetComponent<PlayerCollisionManager>();
            if (manager == null)
                manager = bootstrap.gameObject.AddComponent<PlayerCollisionManager>();
            manager.Configure(network, lobby);
        }

        private static void ConfigureLobbyPreview(Scene scene)
        {
            var bootstrap = scene.GetRootGameObjects().Select(root => root.GetComponent<LobbyBootstrap>())
                .Single(component => component != null);
            var preview = bootstrap.GetComponent<LobbyPreviewView>();
            if (preview == null)
                preview = bootstrap.gameObject.AddComponent<LobbyPreviewView>();

            var cubes = new GameObject[LobbySettings.PlayerCount];
            var material = GetMaterial(CubeMaterialPath, new Color(0.15f, 0.6f, 0.9f));
            for (int index = 0; index < cubes.Length; index++)
            {
                var name = "Lobby Slot " + index;
                var existing = bootstrap.transform.Find(name);
                if (existing != null)
                {
                    cubes[index] = existing.gameObject;
                    continue;
                }
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                SceneManager.MoveGameObjectToScene(cube, scene);
                cube.transform.SetParent(bootstrap.transform, false);
                cube.transform.localPosition = new Vector3(4.5f - index * 3f, 0.55f, index % 2 == 0 ? 0.8f : -0.8f);
                cube.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.SetActive(false);
                cubes[index] = cube;
            }
            preview.Configure(cubes);
            bootstrap.ConfigurePreview(preview);
        }

        private static Material GetMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    throw new System.InvalidOperationException("The URP Lit shader is unavailable.");
                material = new Material(shader);
                material.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static NetworkPrefabsList GetPrefabRegistry(GameObject prefab, GameObject egg)
        {
            if (prefab == null)
                throw new System.InvalidOperationException("The cube player prefab is missing.");
            const string path = "Assets/Prefabs/NetworkPrefabs.asset";
            var registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(registry, path);
            }
            if (!registry.Contains(prefab))
                registry.Add(new NetworkPrefab { Prefab = prefab });
            if (!registry.Contains(egg))
                registry.Add(new NetworkPrefab { Prefab = egg });
            EditorUtility.SetDirty(registry);
            return registry;
        }
    }
}
