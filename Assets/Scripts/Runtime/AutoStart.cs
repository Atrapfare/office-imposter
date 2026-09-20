using System;
using System.Collections;
using System.Globalization;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace OfficeImposter
{
    // Lets a built player start as host or client from the command line, so the
    // networked loop can be smoke-tested without a human clicking through the menu.
    //   OfficeImposter.exe -host
    //   OfficeImposter.exe -client -address 127.0.0.1
    public class AutoStart : MonoBehaviour
    {
        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool host = false;
            bool client = false;
            string address = null;
            float autoQuitSeconds = 0f;
            string shotPath = null;
            float shotDelay = 6f;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-host":
                        host = true;
                        break;
                    case "-client":
                        client = true;
                        break;
                    case "-address":
                        if (i + 1 < args.Length) address = args[++i];
                        break;
                    case "-shot":
                        if (i + 1 < args.Length) shotPath = args[++i];
                        break;
                    case "-shotdelay":
                        if (i + 1 < args.Length)
                        {
                            float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out shotDelay);
                        }
                        break;
                    case "-autoquit":
                        if (i + 1 < args.Length)
                        {
                            float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out autoQuitSeconds);
                        }
                        break;
                }
            }

            if (shotPath != null) StartCoroutine(CaptureThenQuit(shotPath, shotDelay));
            if (!host && !client) return;

            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[AutoStart] No NetworkManager in scene.");
                return;
            }

            if (!string.IsNullOrEmpty(address))
            {
                var transport = manager.GetComponent<UnityTransport>();
                if (transport != null) transport.SetConnectionData(address, 7777);
            }

            manager.OnClientConnectedCallback += id => Debug.Log($"[AutoStart] connected id={id}");
            manager.OnClientDisconnectCallback += id => Debug.Log($"[AutoStart] disconnected id={id}");

            bool started = host ? manager.StartHost() : manager.StartClient();
            Debug.Log($"[AutoStart] mode={(host ? "host" : "client")} started={started}");

            if (autoQuitSeconds > 0f) Invoke(nameof(ReportAndQuit), autoQuitSeconds);
        }

        // Captures the real player window, overlay UI included, which a RenderTexture
        // camera render in the Editor cannot show.
        static IEnumerator CaptureThenQuit(string path, float delay)
        {
            yield return new WaitForSeconds(delay);

            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[AutoStart] screenshot requested at {path}");

            yield return new WaitForSeconds(2.5f);
            Debug.Log("[AutoStart] screenshot done");
            Application.Quit();
        }

        void ReportAndQuit()
        {
            var manager = NetworkManager.Singleton;
            int connected = manager != null && manager.IsServer ? manager.ConnectedClients.Count : -1;
            var boss = FindFirstObjectByType<BossAI>();
            var local = PlayerStatus.Local;

            Debug.Log($"[AutoStart] report players={PlayerStatus.All.Count} connectedClients={connected} " +
                      $"stations={WorkStation.All.Count} " +
                      $"boss={(boss != null ? boss.transform.position.ToString("F1") : "none")} " +
                      $"suspicion={(local != null ? local.Suspicion.Value.ToString("F1") : "n/a")}");
            Application.Quit();
        }
    }
}
