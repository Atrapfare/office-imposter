using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeImposter
{
    public class NetworkHUD : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        [SerializeField] string address = "127.0.0.1";
        [SerializeField] ushort port = 7777;

        Texture2D _dark;
        Texture2D _fill;
        Texture2D _scrim;
        GUIStyle _title, _body, _small, _banner, _mono, _center;

        bool _showSettings;
        bool _wasWatched;
        bool _wasCaught;
        MeetingSystem.Phase _lastPhase = MeetingSystem.Phase.None;

        void OnDestroy()
        {
            Destroy(_dark);
            Destroy(_fill);
            Destroy(_scrim);
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        void EnsureStyles()
        {
            if (_dark == null) _dark = Solid(new Color(0.05f, 0.06f, 0.09f, 0.88f));
            if (_fill == null) _fill = Solid(Color.white);
            if (_scrim == null) _scrim = Solid(new Color(0.03f, 0.04f, 0.06f, 0.82f));

            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;

            _body = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _body.normal.textColor = new Color(0.87f, 0.90f, 0.95f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _small.normal.textColor = new Color(0.65f, 0.69f, 0.76f);

            _banner = new GUIStyle(GUI.skin.label)
            { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _banner.normal.textColor = Color.white;

            _mono = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mono.normal.textColor = Color.white;

            _center = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter };
        }

        void Update()
        {
            var manager = NetworkManager.Singleton;
            bool connected = manager != null && (manager.IsClient || manager.IsServer);

            if (connected && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                IsPaused = !IsPaused;
                if (IsPaused) CursorLock.Unlock();
                else CursorLock.Lock();
            }

            if (!connected && IsPaused) IsPaused = false;

            DriveAudioFeedback();
        }

        void DriveAudioFeedback()
        {
            var status = PlayerStatus.Local;
            if (status != null)
            {
                if (status.IsWatched && !_wasWatched) AudioDirector.Alert();
                _wasWatched = status.IsWatched;

                if (status.IsCaught && !_wasCaught) AudioDirector.Fired();
                _wasCaught = status.IsCaught;
            }

            var meeting = MeetingSystem.Instance;
            if (meeting != null)
            {
                if (meeting.CurrentPhase != _lastPhase && meeting.CurrentPhase == MeetingSystem.Phase.Announced)
                {
                    AudioDirector.Chime();
                }
                _lastPhase = meeting.CurrentPhase;
            }
        }

        void OnGUI()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return;

            EnsureStyles();

            if (!manager.IsClient && !manager.IsServer)
            {
                DrawMainMenu(manager);
                return;
            }

            DrawHud(manager);

            if (IsPaused) DrawPauseMenu(manager);
            DrawEndScreen();
        }

        void DrawMainMenu(NetworkManager manager)
        {
            if (CursorLock.IsLocked) CursorLock.Unlock();

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _scrim);

            const float width = 460f;
            float height = _showSettings ? 420f : 330f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.DrawTexture(rect, _dark);
            GUILayout.BeginArea(new Rect(rect.x + 28f, rect.y + 24f, rect.width - 56f, rect.height - 48f));

            GUILayout.Label("OFFICE IMPOSTER", _title);
            GUILayout.Label("Du hast keine Ahnung, was dein Job ist. Lass es niemanden merken.", _small);
            GUILayout.Space(16f);

            if (GUILayout.Button("Arbeitstag hosten", GUILayout.Height(42f)))
            {
                ApplyTransport(manager);
                manager.StartHost();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Server-Adresse", _small);
            address = GUILayout.TextField(address, GUILayout.Height(24f));

            GUILayout.Space(6f);
            if (GUILayout.Button("Beitreten", GUILayout.Height(34f)))
            {
                ApplyTransport(manager);
                manager.StartClient();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button(_showSettings ? "Einstellungen schließen" : "Einstellungen", GUILayout.Height(26f)))
            {
                _showSettings = !_showSettings;
            }

            if (_showSettings) DrawSettings();

            GUILayout.EndArea();
        }

        void DrawSettings()
        {
            GUILayout.Space(10f);
            GUILayout.Label($"Lautstärke  {Mathf.RoundToInt(AudioDirector.MasterVolume * 100f)}%", _small);
            AudioDirector.MasterVolume = GUILayout.HorizontalSlider(AudioDirector.MasterVolume, 0f, 1f);

            GUILayout.Space(6f);
            GUILayout.Label($"Maus-Empfindlichkeit  {ThirdPersonCamera.Sensitivity:0.00}", _small);
            ThirdPersonCamera.Sensitivity = GUILayout.HorizontalSlider(ThirdPersonCamera.Sensitivity, 0.03f, 0.35f);
        }

        void ApplyTransport(NetworkManager manager)
        {
            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null) transport.SetConnectionData(address.Trim(), port);
        }

        void DrawHud(NetworkManager manager)
        {
            var status = PlayerStatus.Local;
            var game = GameManager.Instance;

            GUI.DrawTexture(new Rect(0f, 0f, 330f, 104f), _dark);
            GUILayout.BeginArea(new Rect(18f, 14f, 300f, 88f));

            string role = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
            string clock = game != null ? FormatTime(game.TimeRemaining.Value) : "--:--";
            GUILayout.Label($"{role}   ·   Feierabend in {clock}", _body);

            if (status != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"Verdacht   {Mathf.RoundToInt(status.Suspicion.Value)}%", _body);
                DrawBar(GUILayoutUtility.GetRect(280f, 15f), status.SuspicionNormalized);
            }

            GUILayout.EndArea();

            DrawWatchedWarning(status);
            DrawMeeting();
            DrawTaskOrPrompt(status);
        }

        void DrawBar(Rect rect, float normalized)
        {
            GUI.DrawTexture(rect, _scrim);

            Color previous = GUI.color;
            GUI.color = Color.Lerp(new Color(0.36f, 0.80f, 0.47f), new Color(0.93f, 0.26f, 0.26f), normalized);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * normalized, rect.height), _fill);
            GUI.color = previous;
        }

        void DrawWatchedWarning(PlayerStatus status)
        {
            if (status == null || !status.IsWatched || status.IsCaught) return;

            var style = new GUIStyle(_center) { fontSize = 20, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(0.97f, 0.42f, 0.35f, 0.7f + Mathf.PingPong(Time.time * 1.6f, 0.3f));
            GUI.Label(new Rect(0f, 118f, Screen.width, 30f), "DER CHEF SIEHT DICH", style);
        }

        void DrawMeeting()
        {
            var meeting = MeetingSystem.Instance;
            if (meeting == null || meeting.CurrentPhase == MeetingSystem.Phase.None) return;

            var rect = new Rect(Screen.width * 0.5f - 230f, 20f, 460f, 52f);
            GUI.DrawTexture(rect, _dark);

            string text = meeting.CurrentPhase == MeetingSystem.Phase.Announced
                ? $"MEETING in {Mathf.CeilToInt(meeting.Countdown)}s  –  Besprechungsraum (Ost)"
                : $"MEETING läuft  –  noch {Mathf.CeilToInt(meeting.Countdown)}s";

            var style = new GUIStyle(_center) { fontSize = 18, fontStyle = FontStyle.Bold };
            style.normal.textColor = meeting.CurrentPhase == MeetingSystem.Phase.Announced
                ? new Color(0.98f, 0.82f, 0.38f)
                : new Color(0.62f, 0.84f, 0.98f);
            GUI.Label(rect, text, style);

            if (meeting.CurrentPrompt == MeetingSystem.PromptAction.None) return;

            var promptRect = new Rect(Screen.width * 0.5f - 230f, 78f, 460f, 58f);
            GUI.DrawTexture(promptRect, _dark);
            GUI.Label(promptRect, MeetingSystem.DescribePrompt(meeting.CurrentPrompt), _mono);

            var barRect = new Rect(promptRect.x + 14f, promptRect.yMax - 10f, promptRect.width - 28f, 5f);
            DrawBar(barRect, Mathf.Clamp01(meeting.PromptCountdown / 3f));
        }

        void DrawTaskOrPrompt(PlayerStatus status)
        {
            var player = PlayerController.Local;
            if (player == null || status == null || status.IsCaught) return;

            WorkTaskRunner task = player.Task;

            if (task != null && task.IsActive)
            {
                var rect = new Rect(Screen.width * 0.5f - 250f, Screen.height - 132f, 500f, 104f);
                GUI.DrawTexture(rect, _dark);

                GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 22f),
                    $"{task.TaskName}   ·   erledigt: {task.Completed}", _center);

                var typed = new GUIStyle(_mono) { fontSize = 34 };
                typed.normal.textColor = new Color(0.55f, 0.85f, 1f);
                GUI.Label(new Rect(rect.x, rect.y + 32f, rect.width, 40f), FormatSequence(task), typed);

                GUI.Label(new Rect(rect.x, rect.y + 76f, rect.width, 20f), "[E] aufhören", _center);
                return;
            }

            string message = null;
            if (player.NearbyStation != null) message = $"[E]  an {player.NearbyStation.Label} so tun als ob";
            else if (status.IsSeated) message = "Du sitzt im Meeting. Reagiere auf die Aufforderungen.";

            if (message == null) return;

            var promptRect = new Rect(Screen.width * 0.5f - 230f, Screen.height - 86f, 460f, 34f);
            GUI.DrawTexture(promptRect, _dark);
            GUI.Label(promptRect, message, _center);
        }

        static string FormatSequence(WorkTaskRunner task)
        {
            string[] keys = task.Sequence.Split(' ');
            var output = new System.Text.StringBuilder();

            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) output.Append("  ");
                output.Append(i < task.Index ? "·" : keys[i]);
            }

            return output.ToString();
        }

        void DrawPauseMenu(NetworkManager manager)
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _scrim);

            const float width = 400f;
            float height = _showSettings ? 330f : 220f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.DrawTexture(rect, _dark);
            GUILayout.BeginArea(new Rect(rect.x + 26f, rect.y + 22f, rect.width - 52f, rect.height - 44f));

            GUILayout.Label("Pause", _title);
            GUILayout.Space(10f);

            if (GUILayout.Button("Weiter", GUILayout.Height(34f)))
            {
                IsPaused = false;
                CursorLock.Lock();
            }

            GUILayout.Space(6f);
            if (GUILayout.Button(_showSettings ? "Einstellungen schließen" : "Einstellungen", GUILayout.Height(28f)))
            {
                _showSettings = !_showSettings;
            }

            if (_showSettings) DrawSettings();

            GUILayout.Space(8f);
            if (GUILayout.Button("Verlassen", GUILayout.Height(28f)))
            {
                IsPaused = false;
                manager.Shutdown();
            }

            GUILayout.EndArea();
        }

        void DrawEndScreen()
        {
            var status = PlayerStatus.Local;
            var game = GameManager.Instance;

            bool fired = status != null && status.IsCaught;
            bool survived = !fired && game != null && game.DayOver.Value;
            if (!fired && !survived) return;

            GUI.DrawTexture(new Rect(0f, Screen.height * 0.30f, Screen.width, 210f), _scrim);

            Color previous = _banner.normal.textColor;
            _banner.normal.textColor = fired ? new Color(0.96f, 0.36f, 0.34f) : new Color(0.46f, 0.91f, 0.56f);
            GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 56f),
                fired ? "ERWISCHT — DU BIST GEFEUERT" : "FEIERABEND — NIEMAND HAT ETWAS GEMERKT", _banner);
            _banner.normal.textColor = previous;

            var player = PlayerController.Local;
            int done = player != null && player.Task != null ? player.Task.Completed : 0;
            GUI.Label(new Rect(0f, Screen.height * 0.34f + 62f, Screen.width, 26f),
                $"Vorgetäuschte Aufgaben: {done}", _center);

            var buttonRect = new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.34f + 100f, 180f, 36f);
            if (GUI.Button(buttonRect, "Zurück zum Menü") && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }
    }
}
