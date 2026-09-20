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

        const float WallHeight = 3.4f;
        const float WallThickness = 0.3f;
        const float CeilingHeight = 3.35f;

        class Palette
        {
            public Material Floor, Carpet, Wall, Partition, DeskTop, DeskBody, Chair, Screen;
            public Material Cabinet, Plant, Skin, Shirt, Trousers, Suit, SuitDark, Accent;
            public Material Trim, Glass, LightPanel, Metal, Paper, Rubber, Poster, CeilingTile;
            public Material ShirtB, ShirtC, ShirtD;
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
            CreateGlobalVolume();

            var office = new GameObject("Office").transform;
            BuildShell(office, palette);
            BuildCover(office, palette);
            BuildDesks(office, palette);
            BuildMeetingRoom(office, palette);
            BuildBreakRoom(office, palette);
            BuildTrim(office, palette);
            BuildWindows(office, palette);
            BuildCeiling(office, palette);
            BuildCeilingLights(office, palette);
            BuildProps(office, palette);

            CreateSpawnPoints(office);
            Transform[] waypoints = CreateWaypoints(office);

            BakeNavMesh();
            CreateReflectionProbe();
            CreateLightProbes();

            GameObject playerPrefab = CreatePlayerPrefab(palette);
            CreateNetworkManager(playerPrefab);
            CreateBoss(palette, waypoints);
            CreateCoworkers(palette);
            CreateSystems();
            HudBuilder.Build();

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
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);

            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);

            return material;
        }

        static Material Emissive(string name, Color baseColor, Color emission, float smoothness = 0.6f)
        {
            Material material = Mat(name, baseColor, smoothness);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Palette CreatePalette() => new Palette
        {
            Floor = Mat("Floor", new Color(0.52f, 0.50f, 0.46f), 0.03f),
            Carpet = Mat("Carpet", new Color(0.16f, 0.20f, 0.26f), 0.02f),
            Wall = Mat("Wall", new Color(0.84f, 0.83f, 0.80f)),
            Partition = Mat("Partition", new Color(0.44f, 0.50f, 0.56f)),
            DeskTop = Mat("DeskTop", new Color(0.83f, 0.68f, 0.48f)),
            DeskBody = Mat("DeskBody", new Color(0.28f, 0.29f, 0.32f)),
            Chair = Mat("Chair", new Color(0.20f, 0.23f, 0.28f)),
            Screen = Emissive("Screen", new Color(0.05f, 0.07f, 0.10f), new Color(0.30f, 0.52f, 0.72f) * 1.6f, 0.82f),
            Trim = Mat("Trim", new Color(0.72f, 0.71f, 0.69f), 0.35f),
            Glass = Emissive("Glass", new Color(0.72f, 0.82f, 0.90f), new Color(0.62f, 0.74f, 0.92f) * 0.55f, 0.92f),
            LightPanel = Emissive("LightPanel", new Color(0.95f, 0.95f, 0.92f), new Color(1f, 0.97f, 0.88f) * 1.9f, 0.5f),
            Metal = Mat("Metal", new Color(0.70f, 0.72f, 0.75f), 0.62f, 0.85f),
            Paper = Mat("Paper", new Color(0.95f, 0.94f, 0.90f), 0.05f),
            Rubber = Mat("Rubber", new Color(0.12f, 0.12f, 0.14f), 0.08f),
            Poster = Mat("Poster", new Color(0.86f, 0.74f, 0.42f), 0.1f),
            CeilingTile = Mat("CeilingTile", new Color(0.66f, 0.65f, 0.63f), 0.04f),
            Cabinet = Mat("Cabinet", new Color(0.62f, 0.64f, 0.67f), 0.3f, 0.4f),
            Plant = Mat("Plant", new Color(0.29f, 0.53f, 0.31f)),
            Skin = Mat("Skin", new Color(0.93f, 0.76f, 0.62f)),
            Shirt = Mat("Shirt", new Color(0.42f, 0.62f, 0.85f)),
            ShirtB = Mat("ShirtB", new Color(0.78f, 0.52f, 0.36f)),
            ShirtC = Mat("ShirtC", new Color(0.46f, 0.66f, 0.50f)),
            ShirtD = Mat("ShirtD", new Color(0.66f, 0.46f, 0.66f)),
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

        // Single-sided, downward-facing surface: visible from inside the room but
        // backface-culled from above, so the chase camera can rise through it.
        static GameObject DownQuad(string name, Transform parent, Vector3 center, Vector2 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;   // keep the sun lighting the interior
            renderer.receiveShadows = false;                      // lamps sit below it; shadowing it just turns it black

            return go;
        }

        static void BuildCeiling(Transform parent, Palette p)
        {
            var ceiling = new GameObject("Ceiling").transform;
            ceiling.SetParent(parent, false);

            // Tiled rather than one big quad so per-pixel lights interpolate sensibly.
            const float tile = 5f;
            for (float x = -20f; x < 20f; x += tile)
            {
                for (float z = -15f; z < 15f; z += tile)
                {
                    DownQuad($"Tile_{x}_{z}", ceiling,
                        new Vector3(x + tile * 0.5f, CeilingHeight, z + tile * 0.5f),
                        new Vector2(tile, tile), p.CeilingTile);
                }
            }
        }

        static void ConfigureLightingEnvironment()
        {
            // No skybox: the office is an interior, and an open sky above the walls
            // both looks unfinished and blows out the auto-exposed image.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.26f, 0.29f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.21f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.30f, 0.31f);
            RenderSettings.fog = false;
        }

        static void CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.91f);
            light.intensity = 0.85f;
            light.shadows = LightShadows.Soft;
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            // Framing for the main menu, before any player spawns and takes the rig over.
            go.transform.position = new Vector3(-17.5f, 2.3f, -12.5f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(13f, -0.9f, 15f).normalized, Vector3.up);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 220f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);

            go.AddComponent<AudioListener>();
            go.AddComponent<FirstPersonLook>();
        }

        static void BuildShell(Transform parent, Palette p)
        {
            // Floor is built as non-overlapping slabs per zone. An overlaid carpet plane
            // z-fights with the slab beneath it at grazing angles across the long room.
            Box("FloorOpenPlan", parent, new Vector3(-6f, -0.1f, 0f), new Vector3(28f, 0.2f, 30f), p.Carpet);
            Box("FloorMeeting", parent, new Vector3(14f, -0.1f, 7.5f), new Vector3(12f, 0.2f, 15f), p.Carpet);
            Box("FloorBreak", parent, new Vector3(14f, -0.1f, -7.5f), new Vector3(12f, 0.2f, 15f), p.Floor);

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
            GameObject filingCabinet = Box("CabinetA", cover, new Vector3(-18.8f, 1f, 12f), cabinet, p.Cabinet);
            MakeStation(filingCabinet, StationKind.Cabinet, "Aktenschrank", new Vector3(0f, 0.2f, 0f));
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

            // Seated position is the chair, turned to face the monitor.
            var sit = new GameObject("SitAnchor").transform;
            sit.SetParent(desk, false);
            sit.localPosition = new Vector3(0f, 0f, 0.62f);
            sit.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var focus = new GameObject("Focus").transform;
            focus.SetParent(desk, false);
            focus.localPosition = new Vector3(0f, 1.1f, -0.2f);

            var seat = desk.gameObject.AddComponent<Seat>();
            seat.Configure(sit, 1.16f, 120f);

            var station = desk.gameObject.AddComponent<WorkStation>();
            station.Configure(label, anchor, focus);
        }

        static void BuildMeetingRoom(Transform parent, Palette p)
        {
            var room = new GameObject("MeetingRoom").transform;
            room.SetParent(parent, false);

            Box("Table", room, new Vector3(15f, 0.72f, 7.5f), new Vector3(4.6f, 0.1f, 1.9f), p.DeskTop);
            Box("TableLegA", room, new Vector3(13.2f, 0.36f, 7.5f), new Vector3(0.25f, 0.72f, 1.5f), p.DeskBody, false);
            Box("TableLegB", room, new Vector3(16.8f, 0.36f, 7.5f), new Vector3(0.25f, 0.72f, 1.5f), p.DeskBody, false);

            for (int i = 0; i < 3; i++)
            {
                float x = 13.6f + i * 1.4f;
                MakeMeetingSeat(CreateSimpleChair(room, new Vector3(x, 0f, 5.9f), 0f, p));
                MakeMeetingSeat(CreateSimpleChair(room, new Vector3(x, 0f, 9.1f), 180f, p));
            }

            Box("Whiteboard", room, new Vector3(15f, 1.7f, 14.7f), new Vector3(4.5f, 1.4f, 0.1f), p.Wall, false);

            // Where finished errands are handed in.
            Box("DeliveryDeskTop", room, new Vector3(18.4f, 0.74f, 13f), new Vector3(1.6f, 0.08f, 0.8f), p.DeskTop);
            Box("DeliveryDeskBody", room, new Vector3(18.4f, 0.37f, 13f), new Vector3(1.4f, 0.72f, 0.7f), p.DeskBody);
            GameObject tray = Box("ChefAblage", room, new Vector3(18.4f, 0.82f, 13f), new Vector3(0.7f, 0.1f, 0.5f), p.Accent, false);
            MakeStation(tray, StationKind.Delivery, "Ablage des Chefs", new Vector3(0f, 0.15f, 0f));
        }

        static void BuildBreakRoom(Transform parent, Palette p)
        {
            var room = new GameObject("BreakRoom").transform;
            room.SetParent(parent, false);

            Box("Counter", room, new Vector3(19.2f, 0.45f, -8f), new Vector3(1.2f, 0.9f, 8f), p.Cabinet);
            GameObject coffee = Box("CoffeeMachine", room, new Vector3(19.2f, 1.15f, -6f), new Vector3(0.6f, 0.5f, 0.55f), p.DeskBody, false);
            MakeStation(coffee, StationKind.Coffee, "Kaffeemaschine", new Vector3(0f, 0f, 0f));

            Box("TableA", room, new Vector3(13.5f, 0.72f, -5.5f), new Vector3(1.6f, 0.09f, 1.6f), p.DeskTop);
            Box("TableALeg", room, new Vector3(13.5f, 0.36f, -5.5f), new Vector3(0.22f, 0.72f, 0.22f), p.DeskBody, false);
            CreateSimpleChair(room, new Vector3(13.5f, 0f, -7f), 0f, p);
            CreateSimpleChair(room, new Vector3(13.5f, 0f, -4f), 180f, p);

            Box("TableB", room, new Vector3(13.5f, 0.72f, -11f), new Vector3(1.6f, 0.09f, 1.6f), p.DeskTop);
            Box("TableBLeg", room, new Vector3(13.5f, 0.36f, -11f), new Vector3(0.22f, 0.72f, 0.22f), p.DeskBody, false);
            CreateSimpleChair(room, new Vector3(13.5f, 0f, -12.5f), 0f, p);

            Box("Vending", room, new Vector3(10.5f, 1f, -14f), new Vector3(1.1f, 2f, 0.6f), p.Accent);
        }

        static Transform CreateSimpleChair(Transform parent, Vector3 position, float facingY, Palette p)
        {
            var chair = new GameObject("Chair").transform;
            chair.SetParent(parent, false);
            chair.localPosition = position;
            chair.localRotation = Quaternion.Euler(0f, facingY, 0f);

            Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.08f, 0.48f), p.Chair, false);
            Box("Back", chair, new Vector3(0f, 0.72f, 0.21f), new Vector3(0.48f, 0.5f, 0.08f), p.Chair, false);
            Box("Post", chair, new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.08f), p.DeskBody, false);
            return chair;
        }

        static void MakeMeetingSeat(Transform chair)
        {
            var focus = new GameObject("Focus").transform;
            focus.SetParent(chair, false);
            focus.localPosition = new Vector3(0f, 0.7f, 0f);

            var seat = chair.gameObject.AddComponent<Seat>();
            seat.Configure(null, 1.16f, 150f);

            var meetingSeat = chair.gameObject.AddComponent<MeetingSeat>();
            meetingSeat.Configure(focus);
        }

        static JobStation MakeStation(GameObject host, StationKind kind, string label, Vector3 focusOffset)
        {
            var focus = new GameObject("Focus").transform;
            focus.SetParent(host.transform, false);
            focus.localPosition = focusOffset;

            var station = host.AddComponent<JobStation>();
            station.Configure(kind, label, focus);
            return station;
        }

        static void CreateSpawnPoints(Transform parent)
        {
            var root = new GameObject("SpawnPoints").transform;
            root.SetParent(parent, false);

            // Kept clear of the walls and facing into the room: the chase camera sits
            // ~6m behind the player, and a spawn near a wall makes it clip in tight.
            Vector3[] positions =
            {
                new Vector3(-13f, 0f, -3.5f),
                new Vector3(-10f, 0f, -3.5f),
                new Vector3(-7f, 0f, -3.5f),
                new Vector3(-13f, 0f, -5f),
                new Vector3(-10f, 0f, -5f),
                new Vector3(-7f, 0f, -5f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"Spawn_{i + 1}");
                point.transform.SetParent(root, false);
                point.transform.localPosition = positions[i];
                // Facing east down the length of the office, not into a partition.
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
            root.AddComponent<WorkTaskRunner>();
            root.AddComponent<PlayerController>();

            BuildCharacterVisual(root.transform, p.Shirt, p.Skin, p.Trousers, p.Accent);

            // The first-person camera parents itself to this at runtime.
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.62f, 0f);

            var so = new SerializedObject(root.GetComponent<PlayerController>());
            so.FindProperty("head").objectReferenceValue = head;
            so.ApplyModifiedPropertiesWithoutUndo();

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

            var vision = boss.AddComponent<VisionCone>();
            vision.Configure(13f, 80f);

            var ai = boss.AddComponent<BossAI>();
            ai.Configure(waypoints);

            BuildCharacterVisual(boss.transform, p.Suit, p.Skin, p.SuitDark, p.Accent);
        }

        static void CreateCoworkers(Palette p)
        {
            var root = new GameObject("Coworkers").transform;

            var spots = new[]
            {
                new Vector3(-14f, 0f, 4f), new Vector3(-8f, 0f, -4f), new Vector3(-3f, 0f, 5f),
                new Vector3(1f, 0f, -3f), new Vector3(-17f, 0f, -9f),
            };
            var shirts = new[] { p.ShirtB, p.ShirtC, p.ShirtD, p.Shirt, p.ShirtB };

            for (int i = 0; i < spots.Length; i++)
            {
                var go = new GameObject($"Coworker_{i + 1}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = spots[i];

                var collider = go.AddComponent<CapsuleCollider>();
                collider.height = 1.8f;
                collider.radius = 0.32f;
                collider.center = new Vector3(0f, 0.9f, 0f);

                var agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.speed = 1.7f;
                agent.angularSpeed = 220f;
                agent.acceleration = 7f;
                agent.stoppingDistance = 0.4f;
                agent.avoidancePriority = 55 + i;
                agent.enabled = false;

                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();

                var vision = go.AddComponent<VisionCone>();
                vision.Configure(9f, 95f);   // shorter reach than the boss, wider glance

                go.AddComponent<CoworkerAI>();
                BuildCharacterVisual(go.transform, shirts[i], p.Skin, p.Trousers, null);
            }
        }

        static void CreateSystems()
        {
            var go = new GameObject("GameSystems");
            go.AddComponent<NetworkObject>();
            go.AddComponent<GameManager>();
            go.AddComponent<MeetingSystem>();
            go.AddComponent<AssignmentSystem>();
            go.AddComponent<AudioDirector>();
            go.AddComponent<AutoStart>();
        }

        static void CreateGlobalVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GraphicsSetup.ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning("[OfficeSceneBuilder] Post-processing profile missing; run Apply Graphics Settings first.");
                return;
            }

            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        static void BuildTrim(Transform parent, Palette p)
        {
            var trim = new GameObject("Trim").transform;
            trim.SetParent(parent, false);

            // Skirting boards give the flat walls a readable floor line.
            Box("SkirtNorth", trim, new Vector3(0f, 0.07f, 14.8f), new Vector3(40f, 0.14f, 0.12f), p.Trim, false);
            Box("SkirtSouth", trim, new Vector3(0f, 0.07f, -14.8f), new Vector3(40f, 0.14f, 0.12f), p.Trim, false);
            Box("SkirtWest", trim, new Vector3(-19.8f, 0.07f, 0f), new Vector3(0.12f, 0.14f, 30f), p.Trim, false);
            Box("SkirtEast", trim, new Vector3(19.8f, 0.07f, 0f), new Vector3(0.12f, 0.14f, 30f), p.Trim, false);

            // Door frames around the two openings in the internal divider.
            Box("DoorFrameTop", trim, new Vector3(8f, 2.3f, 0f), new Vector3(0.45f, 0.25f, 4.2f), p.Trim);
            Box("DoorFrameA", trim, new Vector3(8f, 1.1f, 2.05f), new Vector3(0.45f, 2.2f, 0.16f), p.Trim, false);
            Box("DoorFrameB", trim, new Vector3(8f, 1.1f, -2.05f), new Vector3(0.45f, 2.2f, 0.16f), p.Trim, false);

            Box("WallClock", trim, new Vector3(-19.7f, 2.5f, 0f), new Vector3(0.1f, 0.55f, 0.55f), p.Trim, false);
            Box("PosterA", trim, new Vector3(-19.7f, 2.1f, -8f), new Vector3(0.06f, 1f, 1.4f), p.Poster, false);
            Box("PosterB", trim, new Vector3(-2f, 2.1f, 14.7f), new Vector3(1.4f, 1f, 0.06f), p.Poster, false);
        }

        static void BuildWindows(Transform parent, Palette p)
        {
            var windows = new GameObject("Windows").transform;
            windows.SetParent(parent, false);

            // South wall glazing, with a soft fill light per bay so the room reads as daylit.
            for (int i = 0; i < 4; i++)
            {
                float x = -15f + i * 8f;
                Box($"GlassS_{i}", windows, new Vector3(x, 1.95f, -14.85f), new Vector3(5.2f, 1.8f, 0.08f), p.Glass, false);
                Box($"FrameS_{i}", windows, new Vector3(x, 1.95f, -14.78f), new Vector3(5.4f, 0.1f, 0.12f), p.Trim, false);
                Box($"SillS_{i}", windows, new Vector3(x, 1.0f, -14.7f), new Vector3(5.4f, 0.1f, 0.3f), p.Trim, false);

                if (i % 2 == 0) CreateFillLight($"WindowLightS_{i}", new Vector3(x, 2.1f, -13.6f), new Color(0.72f, 0.82f, 1f), 0.9f, 8f);
            }

            for (int i = 0; i < 3; i++)
            {
                float z = -8f + i * 8f;
                Box($"GlassW_{i}", windows, new Vector3(-19.85f, 1.95f, z), new Vector3(0.08f, 1.8f, 5.2f), p.Glass, false);
                Box($"SillW_{i}", windows, new Vector3(-19.7f, 1.0f, z), new Vector3(0.3f, 0.1f, 5.4f), p.Trim, false);

                if (i == 1) CreateFillLight($"WindowLightW_{i}", new Vector3(-18.6f, 2.1f, z), new Color(0.72f, 0.82f, 1f), 0.9f, 8f);
            }
        }

        static void CreateFillLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;   // fill only; the directional carries the shadowing
            light.renderMode = LightRenderMode.ForceVertex;
        }

        static void BuildCeilingLights(Transform parent, Palette p)
        {
            var fixtures = new GameObject("CeilingLights").transform;
            fixtures.SetParent(parent, false);

            // Staggered and sparse on purpose: overlapping fixtures wash the room flat,
            // and the boss-evasion loop needs pools of light with shadow between them.
            Vector3[] positions =
            {
                new Vector3(-16f, 0f, 8f), new Vector3(-5f, 0f, 8f),
                new Vector3(-11f, 0f, -7f), new Vector3(1f, 0f, -8f),
                new Vector3(-8f, 0f, 0.5f),
                new Vector3(15f, 0f, 7.5f), new Vector3(15f, 0f, -8f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 at = positions[i];
                var fixtureRoot = new GameObject($"Fixture_{i}").transform;
                fixtureRoot.SetParent(fixtures, false);
                fixtureRoot.localPosition = at;

                // Flush-mounted into the ceiling so nothing dangles above it.
                Box("Housing", fixtureRoot, new Vector3(0f, CeilingHeight - 0.04f, 0f), new Vector3(2.35f, 0.08f, 0.66f), p.Metal, false);
                Box("Panel", fixtureRoot, new Vector3(0f, CeilingHeight - 0.09f, 0f), new Vector3(2.2f, 0.03f, 0.55f), p.LightPanel, false);

                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(fixtureRoot, false);
                lightGo.transform.localPosition = new Vector3(0f, CeilingHeight - 0.2f, 0f);

                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 0.85f;
                light.range = 8.5f;
                // A few shadow casters give the furniture real contact shadows; the rest
                // stay cheap fill so the additional-light budget holds.
                light.shadows = i < 3 ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = 0.75f;
                light.renderMode = LightRenderMode.ForcePixel;
            }
        }

        static void BuildProps(Transform parent, Palette p)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(parent, false);

            // Printer corner
            var printer = new GameObject("Printer").transform;
            printer.SetParent(props, false);
            printer.localPosition = new Vector3(4.5f, 0f, 12.5f);
            Box("Base", printer, new Vector3(0f, 0.4f, 0f), new Vector3(1.1f, 0.8f, 0.8f), p.Cabinet);
            Box("Body", printer, new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.35f, 0.7f), p.Rubber, false);
            Box("Tray", printer, new Vector3(0f, 0.86f, 0.42f), new Vector3(0.7f, 0.04f, 0.3f), p.Paper, false);
            Box("PaperStack", printer, new Vector3(0.62f, 0.86f, 0f), new Vector3(0.3f, 0.12f, 0.42f), p.Paper, false);
            MakeStation(printer.gameObject, StationKind.Printer, "Drucker", new Vector3(0f, 1f, 0f));

            // Water cooler
            var cooler = new GameObject("WaterCooler").transform;
            cooler.SetParent(props, false);
            cooler.localPosition = new Vector3(-19f, 0f, -5f);
            Box("Body", cooler, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 1.1f, 0.5f), p.Cabinet);
            Box("Bottle", cooler, new Vector3(0f, 1.4f, 0f), new Vector3(0.42f, 0.6f, 0.42f), p.Glass, false);
            MakeStation(cooler.gameObject, StationKind.WaterCooler, "Wasserspender", new Vector3(0f, 1.2f, 0f));

            // Desk clutter: a mug and a paper stack per workstation row.
            float[] columns = { -16f, -10f, -4f, 2f };
            foreach (float x in columns)
            {
                foreach (float z in new[] { 7f, -7f })
                {
                    float inward = z > 0f ? -1f : 1f;
                    Box("Mug", props, new Vector3(x + 0.62f, 0.83f, z + 0.18f * inward), new Vector3(0.12f, 0.13f, 0.12f), p.Accent, false);
                    Box("Papers", props, new Vector3(x - 0.6f, 0.79f, z + 0.1f * inward), new Vector3(0.32f, 0.04f, 0.24f), p.Paper, false);
                }
            }

            Box("Whiteboard", props, new Vector3(-7f, 1.9f, 14.7f), new Vector3(3.6f, 1.3f, 0.07f), p.Paper, false);
            Box("WhiteboardFrame", props, new Vector3(-7f, 1.9f, 14.75f), new Vector3(3.8f, 1.45f, 0.05f), p.Metal, false);
        }

        static void CreateReflectionProbe()
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.position = new Vector3(-6f, 1.8f, 0f);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.size = new Vector3(42f, 6f, 32f);
            probe.resolution = 256;
            probe.cullingMask = ~0;
            probe.intensity = 0.35f;   // full-strength env reflection washes out matte surfaces
        }

        static void CreateLightProbes()
        {
            var go = new GameObject("LightProbes");
            var group = go.AddComponent<LightProbeGroup>();

            var positions = new List<Vector3>();
            for (float x = -18f; x <= 18f; x += 6f)
            {
                for (float z = -12f; z <= 12f; z += 6f)
                {
                    positions.Add(new Vector3(x, 0.6f, z));
                    positions.Add(new Vector3(x, 2.4f, z));
                }
            }

            group.probePositions = positions.ToArray();
        }

        static void RegisterScene()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
