using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class MaterialAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Office.unity", OpenSceneMode.Single);

            string[] names = { "FloorOpenPlan", "FloorMeeting", "FloorBreak", "WallNorth", "Tile_-20_-15" };
            foreach (string name in names)
            {
                GameObject go = GameObject.Find(name) ?? FindDeep(name);
                if (go == null)
                {
                    Debug.Log($"[Audit] {name}: NOT FOUND");
                    continue;
                }

                var renderer = go.GetComponent<MeshRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                string color = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor").ToString("F2")
                    : "n/a";

                Debug.Log($"[Audit] {name}: material={(material != null ? material.name : "NULL")} base={color}");
            }

            EditorApplication.Exit(0);
        }

        static GameObject FindDeep(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}
