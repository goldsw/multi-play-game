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
            var fishPrefab = SkillSceneSetup.PrepareFish();
            var dogPrefab = SkillSceneSetup.PrepareDog();
            var taggerPrefab = SkillSceneSetup.PrepareTaggerTrap();
            var itemPrefabs = ItemSceneSetup.PreparePrefabs();
            if (File.Exists(ScenePath))
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    ConfigurePlayer(contents);
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
                var cachedRegistry = GetPrefabRegistry(
                    new[] { cachedPrefab, eggPrefab, fishPrefab, dogPrefab, taggerPrefab }
                        .Concat(itemPrefabs).ToArray());
                var loadedScene = SceneManager.GetSceneByPath(ScenePath);
                var wasLoaded = loadedScene.IsValid() && loadedScene.isLoaded;
                if (!wasLoaded)
                    loadedScene = EditorSceneManager.OpenScene(
                        ScenePath, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var sceneNetwork = loadedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<NetworkManager>()).Single();
                sceneNetwork.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                sceneNetwork.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(cachedRegistry);
                ConfigureLobbyPreview(loadedScene);
                LobbyCanvasSetup.Configure(loadedScene);
                InGameSceneSetup.Configure(loadedScene, cachedPrefab.GetComponent<NetworkObject>());
                var existingState = loadedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SessionNetworkState>()).Single();
                if (!existingState.TryGetComponent<MatchResultStore>(out _))
                    existingState.gameObject.AddComponent<MatchResultStore>();
                ResultSceneSetup.Configure();
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
            ConfigurePlayer(cube);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(cube, PrefabPath);
            Object.DestroyImmediate(cube);
            var prefabs = GetPrefabRegistry(new[] { playerPrefab, eggPrefab, fishPrefab, dogPrefab, taggerPrefab }
                .Concat(itemPrefabs).ToArray());

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

            var stateObject = new GameObject("SessionNetworkState");
            stateObject.AddComponent<NetworkObject>();
            var state = stateObject.AddComponent<SessionNetworkState>();
            stateObject.AddComponent<MatchResultStore>();

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
            roots.AddComponent<LobbyBootstrap>().Configure(network, state, manager, presenter, view);
            ConfigureLobbyPreview(scene);
            LobbyCanvasSetup.Configure(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            InGameSceneSetup.Configure(scene, playerPrefab.GetComponent<NetworkObject>());
            ResultSceneSetup.Configure();

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
                new[] { ScenePath, InGameSceneSetup.ScenePath, ResultSceneSetup.ScenePath },
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

        private static void ConfigurePlayer(GameObject player)
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (input == null)
                throw new System.InvalidOperationException("The player input actions asset is missing.");
            if (!player.TryGetComponent<WallContactResponse>(out _))
                player.AddComponent<WallContactResponse>();
            var movement = player.GetComponent<RigidbodyMovement>();
            if (movement == null)
                movement = player.AddComponent<RigidbodyMovement>();
            movement.Configure(input);
            if (!player.TryGetComponent<PlayerPositionCorrection>(out _))
                player.AddComponent<PlayerPositionCorrection>();
            SkillSceneSetup.ConfigurePlayer(player, input);
            ItemSceneSetup.ConfigurePlayer(player, input);
            if (!player.TryGetComponent<Test_eliminated>(out var testEliminated))
                testEliminated = player.AddComponent<Test_eliminated>();
            testEliminated.Configure(input);
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

        private static NetworkPrefabsList GetPrefabRegistry(params GameObject[] prefabs)
        {
            const string path = "Assets/Prefabs/NetworkPrefabs.asset";
            var registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(registry, path);
            }
            foreach (var prefab in prefabs)
            {
                if (prefab == null)
                    throw new System.InvalidOperationException("A network prefab is missing.");
                if (!registry.Contains(prefab))
                    registry.Add(new NetworkPrefab { Prefab = prefab });
            }
            EditorUtility.SetDirty(registry);
            return registry;
        }
    }
}
