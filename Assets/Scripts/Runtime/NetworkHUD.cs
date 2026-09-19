using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace OfficeImposter
{
    public class NetworkHUD : MonoBehaviour
    {
        [SerializeField] string address = "127.0.0.1";
        [SerializeField] ushort port = 7777;

        Texture2D _barBackground;
        Texture2D _barFill;
        Texture2D _panel;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _banner;

        void OnDestroy()
        {
            Destroy(_barBackground);
            Destroy(_barFill);
            Destroy(_panel);
        }

        static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        void EnsureStyles()
        {
            if (_barBackground == null) _barBackground = SolidTexture(new Color(0f, 0f, 0f, 0.55f));
            if (_barFill == null) _barFill = SolidTexture(Color.white);
            if (_panel == null) _panel = SolidTexture(new Color(0.05f, 0.06f, 0.09f, 0.88f));

            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                _title.normal.textColor = Color.white;
            }
            if (_body == null)
            {
                _body = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _body.normal.textColor = new Color(0.85f, 0.88f, 0.94f);
            }
            if (_banner == null)
            {
                _banner = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 40,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _banner.normal.textColor = Color.white;
            }
        }

        void OnGUI()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return;

            EnsureStyles();

            if (!manager.IsClient && !manager.IsServer) DrawConnectMenu(manager);
            else DrawGameHud(manager);
        }

        void DrawConnectMenu(NetworkManager manager)
        {
            if (CursorLock.IsLocked) CursorLock.Unlock();

            const float width = 420f;
            const float height = 290f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.DrawTexture(rect, _panel);
            GUILayout.BeginArea(new Rect(rect.x + 26f, rect.y + 22f, rect.width - 52f, rect.height - 44f));

            GUILayout.Label("OFFICE IMPOSTER", _title);
            GUILayout.Space(4f);
            GUILayout.Label("Tu so, als würdest du arbeiten. Lass dich nicht erwischen.", _body);
            GUILayout.Space(14f);

            if (GUILayout.Button("Host starten", GUILayout.Height(40f)))
            {
                ApplyTransport(manager);
                manager.StartHost();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Server-Adresse", _body);
            address = GUILayout.TextField(address, GUILayout.Height(24f));

            GUILayout.Space(6f);
            if (GUILayout.Button("Als Client beitreten", GUILayout.Height(36f)))
            {
                ApplyTransport(manager);
                manager.StartClient();
            }

            GUILayout.EndArea();
        }

        void ApplyTransport(NetworkManager manager)
        {
            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null) transport.SetConnectionData(address.Trim(), port);
        }

        void DrawGameHud(NetworkManager manager)
        {
            var status = PlayerStatus.Local;
            var game = GameManager.Instance;

            GUI.DrawTexture(new Rect(0f, 0f, 320f, 96f), _panel);
            GUILayout.BeginArea(new Rect(16f, 12f, 300f, 80f));

            string role = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
            string clock = game != null ? FormatTime(game.TimeRemaining.Value) : "--:--";
            GUILayout.Label($"{role}  ·  Feierabend in {clock}", _body);

            if (status != null)
            {
                GUILayout.Space(4f);
                GUILayout.Label($"Verdacht  {Mathf.RoundToInt(status.Suspicion.Value)}%", _body);
                DrawSuspicionBar(GUILayoutUtility.GetRect(268f, 14f), status.SuspicionNormalized);
            }

            GUILayout.EndArea();

            DrawPrompt(status);
            DrawBanner(status, game);
        }

        void DrawSuspicionBar(Rect rect, float normalized)
        {
            GUI.DrawTexture(rect, _barBackground);

            Color previous = GUI.color;
            GUI.color = Color.Lerp(new Color(0.35f, 0.78f, 0.45f), new Color(0.92f, 0.28f, 0.28f), normalized);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * normalized, rect.height), _barFill);
            GUI.color = previous;
        }

        void DrawPrompt(PlayerStatus status)
        {
            var player = PlayerController.Local;
            if (player == null || status == null || status.IsCaught) return;

            string message = null;
            if (status.IsWorking) message = "Du tust beschäftigt…  [E] loslassen zum Aufhören";
            else if (player.NearbyStation != null) message = $"[E] halten – an {player.NearbyStation.Label} so tun als ob";

            if (message == null) return;

            var rect = new Rect(Screen.width * 0.5f - 260f, Screen.height - 96f, 520f, 34f);
            GUI.DrawTexture(rect, _panel);

            var centered = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(rect, message, centered);
        }

        void DrawBanner(PlayerStatus status, GameManager game)
        {
            string text = null;
            Color color = Color.white;

            if (status != null && status.IsCaught)
            {
                text = "ERWISCHT — DU BIST GEFEUERT";
                color = new Color(0.95f, 0.35f, 0.35f);
            }
            else if (game != null && game.DayOver.Value)
            {
                text = "FEIERABEND — NIEMAND HAT ETWAS GEMERKT";
                color = new Color(0.45f, 0.9f, 0.55f);
            }

            if (text == null) return;

            Color previous = _banner.normal.textColor;
            _banner.normal.textColor = color;
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 60f), text, _banner);
            _banner.normal.textColor = previous;
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }
    }
}
