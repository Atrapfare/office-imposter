using System;
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
                    case "-autoquit":
                        if (i + 1 < args.Length)
                        {
                            float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out autoQuitSeconds);
                        }
                        break;
                }
            }

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
