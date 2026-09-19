using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ProjectBootstrap
{
    using OfficeImposter;

    // Rebuilds the playable office scene from scratch so the level is reproducible
    // from source rather than hand-authored YAML.
    public static class OfficeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Office.unity";
        const string PrefabDir = "Assets/Prefabs";
        const string MaterialDir = "Assets/Materials";
        const string NavMeshAssetPath = "Assets/Scenes/Office_NavMesh.asset";

        const float WallHeight = 3.2f;
        const float WallThickness = 0.3f;

        class Palette
        {
            public Material Floor, Carpet, Wall, Partition, DeskTop, DeskBody, Chair, Screen;
            public Material Cabinet, Plant, Skin, Shirt, Trousers, Suit, SuitDark, Accent;
        }

        [MenuItem("Office Imposter/Rebuild Office Scene")]
        public static void Build()
        {
            EnsureFolders();
            Palette palette = CreatePalette();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureLightingEnvironment();
            CreateDirectionalLight();
            CreateCamera();

            var office = new GameObject("Office").transform;
            BuildShell(office, palette);
            BuildCover(office, palette);
            BuildDesks(office, palette);
            BuildMeetingRoom(office, palette);
            BuildBreakRoom(office, palette);

            CreateSpawnPoints(office);
            Transform[] waypoints = CreateWaypoints(office);

            BakeNavMesh();

            GameObject playerPrefab = CreatePlayerPrefab(palette);
            CreateNetworkManager(playerPrefab);
            CreateBoss(palette, waypoints);
            CreateSystems();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene();
            AssetDatabase.SaveAssets();

            Debug.Log($"[OfficeSceneBuilder] Built {ScenePath}");
        }

        // Entry point for -executeMethod.
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[OfficeSceneBuilder] Failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
        }

        static Material Mat(string name, Color color, float smoothness = 0.12f, float metallic = 0f)
        {
            string path = $"{MaterialDir}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader);

            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);

            return material;
        }

        static Palette CreatePalette() => new Palette
        {
            Floor = Mat("Floor", new Color(0.78f, 0.76f, 0.71f)),
            Carpet = Mat("Carpet", new Color(0.36f, 0.42f, 0.48f)),
            Wall = Mat("Wall", new Color(0.90f, 0.89f, 0.86f)),
            Partition = Mat("Partition", new Color(0.55f, 0.62f, 0.66f)),
            DeskTop = Mat("DeskTop", new Color(0.83f, 0.68f, 0.48f)),
            DeskBody = Mat("DeskBody", new Color(0.28f, 0.29f, 0.32f)),
            Chair = Mat("Chair", new Color(0.20f, 0.23f, 0.28f)),
            Screen = Mat("Screen", new Color(0.13f, 0.18f, 0.24f), 0.75f),
            Cabinet = Mat("Cabinet", new Color(0.62f, 0.64f, 0.67f), 0.3f, 0.4f),
            Plant = Mat("Plant", new Color(0.29f, 0.53f, 0.31f)),
            Skin = Mat("Skin", new Color(0.93f, 0.76f, 0.62f)),
            Shirt = Mat("Shirt", new Color(0.42f, 0.62f, 0.85f)),
            Trousers = Mat("Trousers", new Color(0.26f, 0.30f, 0.38f)),
            Suit = Mat("Suit", new Color(0.21f, 0.20f, 0.26f)),
            SuitDark = Mat("SuitDark", new Color(0.14f, 0.13f, 0.18f)),
            Accent = Mat("Accent", new Color(0.85f, 0.32f, 0.30f)),
        };

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;

            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }

        static void ConfigureLightingEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.49f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.31f, 0.30f);
            RenderSettings.fog = false;
        }

        static void CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.91f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(-16f, 6f, -6f);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 220f;

            go.AddComponent<AudioListener>();
            go.AddComponent<ThirdPersonCamera>();
        }

        static void BuildShell(Transform parent, Palette p)
        {
            Box("Floor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(40f, 0.2f, 30f), p.Floor);
            Box("CarpetOpenArea", parent, new Vector3(-6f, 0.005f, 0f), new Vector3(27.5f, 0.02f, 29.4f), p.Carpet, false);

            float y = WallHeight * 0.5f;
            Box("WallNorth", parent, new Vector3(0f, y, 15f), new Vector3(40.3f, WallHeight, WallThickness), p.Wall);
            Box("WallSouth", parent, new Vector3(0f, y, -15f), new Vector3(40.3f, WallHeight, WallThickness), p.Wall);
            Box("WallWest", parent, new Vector3(-20f, y, 0f), new Vector3(WallThickness, WallHeight, 30f), p.Wall);
            Box("WallEast", parent, new Vector3(20f, y, 0f), new Vector3(WallThickness, WallHeight, 30f), p.Wall);

            // Divider between the open-plan area and the side rooms, with a doorway at z 0.
            Box("DividerNorth", parent, new Vector3(8f, y, 8.5f), new Vector3(WallThickness, WallHeight, 13f), p.Wall);
            Box("DividerSouth", parent, new Vector3(8f, y, -8.5f), new Vector3(WallThickness, WallHeight, 13f), p.Wall);

            // Splits the side rooms, leaving an x 8..10 corridor between them.
            Box("RoomSplit", parent, new Vector3(15f, y, 0f), new Vector3(10f, WallHeight, WallThickness), p.Wall);
        }

        static void BuildCover(Transform parent, Palette p)
        {
            var cover = new GameObject("Cover").transform;
            cover.SetParent(parent, false);

            // Chest-high partitions break the boss's line of sight without blocking pathing.
            Box("PartitionA", cover, new Vector3(-13f, 0.9f, 0f), new Vector3(6f, 1.8f, 0.25f), p.Partition);
            Box("PartitionB", cover, new Vector3(-1f, 0.9f, 0f), new Vector3(6f, 1.8f, 0.25f), p.Partition);
            Box("PartitionC", cover, new Vector3(-19f, 0.9f, 6f), new Vector3(0.25f, 1.8f, 5f), p.Partition);

            Vector3 cabinet = new Vector3(1.1f, 2f, 0.55f);
            Box("CabinetA", cover, new Vector3(-18.8f, 1f, 12f), cabinet, p.Cabinet);
            Box("CabinetB", cover, new Vector3(-18.8f, 1f, -12f), cabinet, p.Cabinet);
            Box("CabinetC", cover, new Vector3(6.5f, 1f, 12f), cabinet, p.Cabinet);
            Box("CabinetD", cover, new Vector3(6.5f, 1f, -12f), cabinet, p.Cabinet);
            Box("CabinetE", cover, new Vector3(-7f, 1f, 13.5f), new Vector3(2.4f, 2f, 0.55f), p.Cabinet);

            CreatePlant(cover, new Vector3(-19f, 0f, 3f), p);
            CreatePlant(cover, new Vector3(6.6f, 0f, 4f), p);
            CreatePlant(cover, new Vector3(-10f, 0f, -13.6f), p);
        }

        static void CreatePlant(Transform parent, Vector3 position, Palette p)
        {
            var root = new GameObject("Plant").transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            Box("Pot", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.45f, 0.44f, 0.45f), p.DeskBody);
            Box("Leaves", root, new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.9f, 0.7f), p.Plant, false);
        }

        static void BuildDesks(Transform parent, Palette p)
        {
            var desks = new GameObject("Desks").transform;
            desks.SetParent(parent, false);

            float[] columns = { -16f, -10f, -4f, 2f };
            int index = 1;

            foreach (float x in columns)
            {
                CreateDesk(desks, new Vector3(x, 0f, 7f), 180f, $"Platz {index++}", p);
                CreateDesk(desks, new Vector3(x, 0f, -7f), 0f, $"Platz {index++}", p);
            }
        }

        static void CreateDesk(Transform parent, Vector3 position, float facingY, string label, Palette p)
        {
            var desk = new GameObject($"Desk_{label}").transform;
            desk.SetParent(parent, false);
            desk.localPosition = position;
            desk.localRotation = Quaternion.Euler(0f, facingY, 0f);

            Box("Top", desk, new Vector3(0f, 0.74f, 0f), new Vector3(1.8f, 0.07f, 0.85f), p.DeskTop);
            Box("PedestalLeft", desk, new Vector3(-0.74f, 0.37f, 0f), new Vector3(0.26f, 0.72f, 0.78f), p.DeskBody);
            Box("PedestalRight", desk, new Vector3(0.74f, 0.37f, 0f), new Vector3(0.26f, 0.72f, 0.78f), p.DeskBody);
            Box("MonitorStand", desk, new Vector3(0f, 0.84f, -0.2f), new Vector3(0.1f, 0.14f, 0.1f), p.DeskBody, false);
            Box("Monitor", desk, new Vector3(0f, 1.1f, -0.2f), new Vector3(0.64f, 0.42f, 0.05f), p.Screen, false);
            Box("Keyboard", desk, new Vector3(0f, 0.79f, 0.14f), new Vector3(0.5f, 0.03f, 0.18f), p.DeskBody, false);

            var chair = new GameObject("Chair").transform;
            chair.SetParent(desk, false);
            chair.localPosition = new Vector3(0f, 0f, 0.8f);
            Box("Post", chair, new Vector3(0f, 0.22f, 0f), new Vector3(0.09f, 0.44f, 0.09f), p.DeskBody, false);
            Box("Seat", chair, new Vector3(0f, 0.46f, 0f), new Vector3(0.5f, 0.09f, 0.5f), p.Chair, false);
            Box("Back", chair, new Vector3(0f, 0.74f, 0.22f), new Vector3(0.5f, 0.52f, 0.08f), p.Chair, false);

            var anchor = new GameObject("StandAnchor").transform;
            anchor.SetParent(desk, false);
            anchor.localPosition = new Vector3(0f, 0f, 1.35f);

            var station = desk.gameObject.AddComponent<WorkStation>();
            station.Configure(label, anchor);
        }

        static void BuildMeetingRoom(Transform parent, Palette p)
        {
            var room = new GameObject("MeetingRoom").transform;
            room.SetParent(parent, false);

            Box("Carpet", room, new Vector3(15f, 0.005f, 7.5f), new Vector3(9.5f, 0.02f, 14.5f), p.Carpet, false);
            Box("Table", room, new Vector3(15f, 0.72f, 7.5f), new Vector3(4.6f, 0.1f, 1.9f), p.DeskTop);
            Box("TableLegA", room, new Vector3(13.2f, 0.36f, 7.5f), new Vector3(0.25f, 0.72f, 1.5f), p.DeskBody, false);
            Box("TableLegB", room, new Vector3(16.8f, 0.36f, 7.5f), new Vector3(0.25f, 0.72f, 1.5f), p.DeskBody, false);

            for (int i = 0; i < 3; i++)
            {
                float x = 13.6f + i * 1.4f;
                CreateSimpleChair(room, new Vector3(x, 0f, 5.9f), 0f, p);
                CreateSimpleChair(room, new Vector3(x, 0f, 9.1f), 180f, p);
            }

            Box("Whiteboard", room, new Vector3(15f, 1.7f, 14.7f), new Vector3(4.5f, 1.4f, 0.1f), p.Wall, false);
        }

        static void BuildBreakRoom(Transform parent, Palette p)
        {
            var room = new GameObject("BreakRoom").transform;
            room.SetParent(parent, false);

            Box("Counter", room, new Vector3(19.2f, 0.45f, -8f), new Vector3(1.2f, 0.9f, 8f), p.Cabinet);
            Box("CoffeeMachine", room, new Vector3(19.2f, 1.15f, -6f), new Vector3(0.6f, 0.5f, 0.55f), p.DeskBody, false);

            Box("TableA", room, new Vector3(13.5f, 0.72f, -5.5f), new Vector3(1.6f, 0.09f, 1.6f), p.DeskTop);
            Box("TableALeg", room, new Vector3(13.5f, 0.36f, -5.5f), new Vector3(0.22f, 0.72f, 0.22f), p.DeskBody, false);
            CreateSimpleChair(room, new Vector3(13.5f, 0f, -7f), 0f, p);
            CreateSimpleChair(room, new Vector3(13.5f, 0f, -4f), 180f, p);

            Box("TableB", room, new Vector3(13.5f, 0.72f, -11f), new Vector3(1.6f, 0.09f, 1.6f), p.DeskTop);
            Box("TableBLeg", room, new Vector3(13.5f, 0.36f, -11f), new Vector3(0.22f, 0.72f, 0.22f), p.DeskBody, false);
            CreateSimpleChair(room, new Vector3(13.5f, 0f, -12.5f), 0f, p);

            Box("Vending", room, new Vector3(10.5f, 1f, -14f), new Vector3(1.1f, 2f, 0.6f), p.Accent);
        }

        static void CreateSimpleChair(Transform parent, Vector3 position, float facingY, Palette p)
        {
            var chair = new GameObject("Chair").transform;
            chair.SetParent(parent, false);
            chair.localPosition = position;
            chair.localRotation = Quaternion.Euler(0f, facingY, 0f);

            Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.08f, 0.48f), p.Chair, false);
            Box("Back", chair, new Vector3(0f, 0.72f, 0.21f), new Vector3(0.48f, 0.5f, 0.08f), p.Chair, false);
            Box("Post", chair, new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.08f), p.DeskBody, false);
        }

        static void CreateSpawnPoints(Transform parent)
        {
            var root = new GameObject("SpawnPoints").transform;
            root.SetParent(parent, false);

            Vector3[] positions =
            {
                new Vector3(-18f, 0f, -2f),
                new Vector3(-18f, 0f, 2f),
                new Vector3(-16f, 0f, -3.5f),
                new Vector3(-16f, 0f, 3.5f),
                new Vector3(-14f, 0f, -2f),
                new Vector3(-14f, 0f, 2f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"Spawn_{i + 1}");
                point.transform.SetParent(root, false);
                point.transform.localPosition = positions[i];
                point.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                point.AddComponent<SpawnPoint>();
            }
        }

        static Transform[] CreateWaypoints(Transform parent)
        {
            var root = new GameObject("BossWaypoints").transform;
            root.SetParent(parent, false);

            Vector3[] positions =
            {
                new Vector3(-16f, 0f, 0f),
                new Vector3(-16f, 0f, 11f),
                new Vector3(2f, 0f, 11f),
                new Vector3(9f, 0f, 0f),
                new Vector3(15f, 0f, 9f),
                new Vector3(15f, 0f, -9f),
                new Vector3(2f, 0f, -11f),
                new Vector3(-16f, 0f, -11f),
            };

            var waypoints = new List<Transform>();
            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"Waypoint_{i + 1}");
                point.transform.SetParent(root, false);
                point.transform.localPosition = positions[i];
                waypoints.Add(point.transform);
            }

            return waypoints.ToArray();
        }

        static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            }
        }

        static void BuildCharacterVisual(Transform parent, Material torso, Material skin, Material legs, Material accent)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);

            Box("Legs", visual, new Vector3(0f, 0.38f, 0f), new Vector3(0.52f, 0.76f, 0.34f), legs, false);
            Box("Torso", visual, new Vector3(0f, 1.05f, 0f), new Vector3(0.64f, 0.6f, 0.38f), torso, false);
            Box("ArmLeft", visual, new Vector3(-0.41f, 1.05f, 0f), new Vector3(0.17f, 0.56f, 0.25f), torso, false);
            Box("ArmRight", visual, new Vector3(0.41f, 1.05f, 0f), new Vector3(0.17f, 0.56f, 0.25f), torso, false);
            Box("Head", visual, new Vector3(0f, 1.57f, 0f), new Vector3(0.42f, 0.42f, 0.42f), skin, false);
            Box("Nose", visual, new Vector3(0f, 1.55f, 0.24f), new Vector3(0.11f, 0.11f, 0.09f), skin, false);

            if (accent != null) Box("Tie", visual, new Vector3(0f, 1.0f, 0.2f), new Vector3(0.1f, 0.42f, 0.03f), accent, false);
        }

        static GameObject CreatePlayerPrefab(Palette p)
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;

            root.AddComponent<NetworkObject>();
            root.AddComponent<ClientNetworkTransform>();
            root.AddComponent<PlayerStatus>();
            root.AddComponent<PlayerController>();

            BuildCharacterVisual(root.transform, p.Shirt, p.Skin, p.Trousers, p.Accent);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Player.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void CreateNetworkManager(GameObject playerPrefab)
        {
            var go = new GameObject("NetworkManager");
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.EnableSceneManagement = true;

            transport.SetConnectionData("127.0.0.1", 7777);
        }

        static void CreateBoss(Palette p, Transform[] waypoints)
        {
            var boss = new GameObject("Boss");
            boss.transform.position = new Vector3(-16f, 0f, 0f);

            var collider = boss.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.35f;
            collider.center = new Vector3(0f, 0.9f, 0f);

            var agent = boss.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = 2.1f;
            agent.angularSpeed = 240f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.4f;
            // NavMeshSurface adds its data during scene load; an agent enabled before
            // that logs "no valid NavMesh". BossAI re-enables it on the server.
            agent.enabled = false;

            boss.AddComponent<NetworkObject>();
            boss.AddComponent<NetworkTransform>();

            var ai = boss.AddComponent<BossAI>();
            ai.Configure(waypoints);

            BuildCharacterVisual(boss.transform, p.Suit, p.Skin, p.SuitDark, p.Accent);
        }

        static void CreateSystems()
        {
            var go = new GameObject("GameSystems");
            go.AddComponent<NetworkObject>();
            go.AddComponent<GameManager>();
            go.AddComponent<NetworkHUD>();
            go.AddComponent<AutoStart>();
        }

        static void RegisterScene()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
