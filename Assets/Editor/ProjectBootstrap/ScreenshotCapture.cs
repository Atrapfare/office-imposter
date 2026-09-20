using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectBootstrap
{
    // Renders the office from fixed vantage points so the look can be reviewed
    // without opening the Editor GUI.
    public static class ScreenshotCapture
    {
        public static void Capture()
        {
            string outputDir = GetArg("-shotDir") ?? "Screenshots";
            Directory.CreateDirectory(outputDir);

            EditorSceneManager.OpenScene("Assets/Scenes/Office.unity", OpenSceneMode.Single);

            var shots = new (string name, Vector3 position, Vector3 lookAt, float fov)[]
            {
                ("01_openplan", new Vector3(-17f, 2.6f, -11f), new Vector3(-6f, 1.0f, 2f), 60f),
                ("02_desks", new Vector3(-12f, 1.9f, -1.5f), new Vector3(-10f, 1.0f, 7f), 58f),
                ("03_overview", new Vector3(-4f, 11f, -19f), new Vector3(-2f, 0f, 2f), 62f),
                ("04_meetingroom", new Vector3(10.5f, 2.6f, 2.5f), new Vector3(15f, 1.0f, 9f), 60f),
                ("05_window", new Vector3(-6f, 1.8f, -6f), new Vector3(-14f, 1.6f, -14f), 58f),
            };

            var cameraGo = new GameObject("CaptureCamera");
            var camera = cameraGo.AddComponent<Camera>();
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.renderShadows = true;

            foreach (var shot in shots)
            {
                camera.transform.position = shot.position;
                camera.transform.rotation = Quaternion.LookRotation((shot.lookAt - shot.position).normalized, Vector3.up);
                camera.fieldOfView = shot.fov;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 250f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);

                var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1
                };

                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;

                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                image.Apply();

                RenderTexture.active = previous;
                camera.targetTexture = null;

                string path = Path.Combine(outputDir, $"{shot.name}.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log($"[Screenshot] wrote {path}");

                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }

            Object.DestroyImmediate(cameraGo);
            Debug.Log("[Screenshot] DONE");
            EditorApplication.Exit(0);
        }

        static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
