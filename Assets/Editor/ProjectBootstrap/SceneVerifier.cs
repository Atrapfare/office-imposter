using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectBootstrap
{
    using OfficeImposter;

    public static class SceneVerifier
    {
        public static void Verify()
        {
            int problems = 0;
            EditorSceneManager.OpenScene("Assets/Scenes/Office.unity", OpenSceneMode.Single);

            var manager = Object.FindFirstObjectByType<NetworkManager>();
            problems += Check(manager != null, "NetworkManager present");
            if (manager != null)
            {
                problems += Check(manager.NetworkConfig.PlayerPrefab != null, "PlayerPrefab assigned");
                problems += Check(manager.NetworkConfig.NetworkTransport != null, "Transport assigned");
            }

            var boss = Object.FindFirstObjectByType<BossAI>();
            problems += Check(boss != null, "Boss present");
            if (boss != null)
            {
                bool onNavMesh = NavMesh.SamplePosition(boss.transform.position, out _, 2f, NavMesh.AllAreas);
                problems += Check(onNavMesh, "Boss spawn is on the NavMesh");
                problems += Check(boss.GetComponent<NetworkObject>() != null, "Boss has NetworkObject");
            }

            var stations = Object.FindObjectsByType<WorkStation>(FindObjectsSortMode.None);
            problems += Check(stations.Length == 8, $"8 work stations (found {stations.Length})");

            var spawns = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            problems += Check(spawns.Length == 6, $"6 spawn points (found {spawns.Length})");
            foreach (var spawn in spawns)
            {
                if (!NavMesh.SamplePosition(spawn.transform.position, out _, 2f, NavMesh.AllAreas))
                {
                    Debug.LogError($"[Verify] FAIL spawn point off NavMesh: {spawn.name}");
                    problems++;
                }
            }

            var coworkers = Object.FindObjectsByType<CoworkerAI>(FindObjectsSortMode.None);
            problems += Check(coworkers.Length == 5, $"5 coworkers (found {coworkers.Length})");
            foreach (var coworker in coworkers)
            {
                if (coworker.GetComponent<VisionCone>() == null)
                {
                    Debug.LogError($"[Verify] FAIL coworker without VisionCone: {coworker.name}");
                    problems++;
                }
                if (!NavMesh.SamplePosition(coworker.transform.position, out _, 2f, NavMesh.AllAreas))
                {
                    Debug.LogError($"[Verify] FAIL coworker off NavMesh: {coworker.name}");
                    problems++;
                }
            }

            var seats = Object.FindObjectsByType<MeetingSeat>(FindObjectsSortMode.None);
            problems += Check(seats.Length == 6, $"6 meeting seats (found {seats.Length})");
            foreach (var seat in seats)
            {
                if (!MeetingSystem.RoomBounds.Contains(seat.transform.position))
                {
                    Debug.LogError($"[Verify] FAIL meeting seat outside room bounds: {seat.name}");
                    problems++;
                }
            }

            problems += Check(Object.FindFirstObjectByType<MeetingSystem>() != null, "MeetingSystem present");
            problems += Check(Object.FindFirstObjectByType<AssignmentSystem>() != null, "AssignmentSystem present");

            var jobStations = Object.FindObjectsByType<JobStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            problems += Check(jobStations.Length >= 5, $"job stations placed ({jobStations.Length})");
            bool hasPrinter = false, hasCoffee = false, hasDelivery = false, hasCabinet = false;
            foreach (var jobStation in jobStations)
            {
                if (jobStation.Kind == StationKind.Printer) hasPrinter = true;
                if (jobStation.Kind == StationKind.Coffee) hasCoffee = true;
                if (jobStation.Kind == StationKind.Delivery) hasDelivery = true;
                if (jobStation.Kind == StationKind.Cabinet) hasCabinet = true;
            }
            problems += Check(hasPrinter && hasCoffee && hasDelivery && hasCabinet,
                "every job route has a station (printer/coffee/delivery/cabinet)");

            var allSeats = Object.FindObjectsByType<Seat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            problems += Check(allSeats.Length >= 14, $"seats placed ({allSeats.Length})");

            var interactables = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            problems += Check(interactables.Length >= 18, $"interactables registered ({interactables.Length})");

            var hudController = Object.FindFirstObjectByType<HudController>();
            if (hudController != null)
            {
                bool bound = hudController.mainMenu != null && hudController.hud != null
                    && hudController.jobPanel != null && hudController.confrontPanel != null
                    && hudController.holdWindow != null && hudController.energyFill != null
                    && hudController.crosshair != null && hudController.endGrade != null
                    && hudController.confrontAnswers != null && hudController.confrontAnswers.Length == 3;
                problems += Check(bound, "HUD references bound");
            }
            problems += Check(Object.FindFirstObjectByType<AudioDirector>() != null, "AudioDirector present");
            problems += Check(Object.FindFirstObjectByType<BossAI>()?.GetComponent<VisionCone>() != null, "Boss has VisionCone");
            problems += Check(Object.FindFirstObjectByType<GameManager>() != null, "GameManager present");
            problems += Check(Object.FindFirstObjectByType<HudController>() != null, "HudController present");
            problems += Check(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null, "EventSystem present");
            var canvas = Object.FindFirstObjectByType<UnityEngine.Canvas>();
            problems += Check(canvas != null, "UI canvas present");
            var texts = Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            problems += Check(texts.Length > 10, $"TMP labels built ({texts.Length})");
            int unfonted = 0;
            foreach (var t in texts) if (t.font == null) unfonted++;
            problems += Check(unfonted == 0, $"all TMP labels have a font ({unfonted} missing)");
            problems += Check(Object.FindFirstObjectByType<FirstPersonLook>() != null, "First-person camera rig present");

            var triangulation = NavMesh.CalculateTriangulation();
            problems += Check(triangulation.vertices.Length > 0,
                $"NavMesh baked ({triangulation.vertices.Length} verts, {triangulation.indices.Length / 3} tris)");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            problems += Check(prefab != null, "Player prefab asset exists");
            if (prefab != null)
            {
                problems += Check(prefab.GetComponent<CharacterController>() != null, "Player has CharacterController");
                problems += Check(prefab.GetComponent<PlayerController>() != null, "Player has PlayerController");
                problems += Check(prefab.GetComponent<PlayerStatus>() != null, "Player has PlayerStatus");
                problems += Check(prefab.GetComponent<ClientNetworkTransform>() != null, "Player has ClientNetworkTransform");
                problems += Check(prefab.GetComponent<WorkTaskRunner>() != null, "Player has WorkTaskRunner");
                problems += Check(prefab.transform.Find("Head") != null, "Player has a Head anchor for the camera");
            }

            Debug.Log($"[Verify] DONE problems={problems}");
            EditorApplication.Exit(problems == 0 ? 0 : 1);
        }

        static int Check(bool condition, string label)
        {
            Debug.Log($"[Verify] {(condition ? "OK  " : "FAIL")} {label}");
            return condition ? 0 : 1;
        }
    }
}
