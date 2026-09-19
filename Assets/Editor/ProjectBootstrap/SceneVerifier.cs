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

            problems += Check(Object.FindFirstObjectByType<GameManager>() != null, "GameManager present");
            problems += Check(Object.FindFirstObjectByType<NetworkHUD>() != null, "NetworkHUD present");
            problems += Check(Object.FindFirstObjectByType<ThirdPersonCamera>() != null, "Camera rig present");

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
