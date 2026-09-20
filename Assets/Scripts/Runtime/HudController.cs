using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OfficeImposter
{
    // Drives the generated uGUI hierarchy. References are bound by HudBuilder when the
    // scene is generated, so nothing here searches by name at runtime.
    public class HudController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        [SerializeField] GameObject mainMenu;
        [SerializeField] GameObject hud;
        [SerializeField] GameObject pauseMenu;
        [SerializeField] GameObject endScreen;

        [SerializeField] TMP_InputField addressField;
        [SerializeField] Button hostButton;
        [SerializeField] Button joinButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button leaveButton;
        [SerializeField] Button endMenuButton;
        // Both the main menu and the pause menu carry a settings block, so these are
        // arrays kept in sync rather than a single pair of controls.
        [SerializeField] Slider[] volumeSliders;
        [SerializeField] Slider[] sensitivitySliders;
        [SerializeField] TextMeshProUGUI[] volumeLabels;
        [SerializeField] TextMeshProUGUI[] sensitivityLabels;

        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] TextMeshProUGUI suspicionText;
        [SerializeField] Image suspicionFill;
        [SerializeField] TextMeshProUGUI watchedText;

        [SerializeField] GameObject meetingPanel;
        [SerializeField] TextMeshProUGUI meetingText;
        [SerializeField] GameObject promptPanel;
        [SerializeField] TextMeshProUGUI promptText;
        [SerializeField] Image promptFill;

        [SerializeField] GameObject bottomPanel;
        [SerializeField] TextMeshProUGUI taskTitle;
        [SerializeField] TextMeshProUGUI taskSequence;
        [SerializeField] TextMeshProUGUI taskHint;

        [SerializeField] TextMeshProUGUI endTitle;
        [SerializeField] TextMeshProUGUI endStats;

        static readonly Color Good = new Color(0.36f, 0.80f, 0.47f);
        static readonly Color Bad = new Color(0.93f, 0.26f, 0.26f);

        readonly StringBuilder _builder = new StringBuilder();
        bool _wasWatched;
        bool _wasCaught;
        MeetingSystem.Phase _lastPhase = MeetingSystem.Phase.None;

        public void Bind(
            GameObject menuRoot, GameObject hudRoot, GameObject pauseRoot, GameObject endRoot,
            TMP_InputField address, Button host, Button join, Button resume, Button leave, Button endMenu,
            Slider[] volumes, Slider[] sensitivities, TextMeshProUGUI[] volumeTexts, TextMeshProUGUI[] sensitivityTexts,
            TextMeshProUGUI status, TextMeshProUGUI suspicion, Image suspicionBar, TextMeshProUGUI watched,
            GameObject meetingRoot, TextMeshProUGUI meeting, GameObject promptRoot, TextMeshProUGUI prompt, Image promptBar,
            GameObject bottomRoot, TextMeshProUGUI title, TextMeshProUGUI sequence, TextMeshProUGUI hint,
            TextMeshProUGUI endTitleText, TextMeshProUGUI endStatsText)
        {
            mainMenu = menuRoot; hud = hudRoot; pauseMenu = pauseRoot; endScreen = endRoot;
            addressField = address; hostButton = host; joinButton = join;
            resumeButton = resume; leaveButton = leave; endMenuButton = endMenu;
            volumeSliders = volumes; sensitivitySliders = sensitivities;
            volumeLabels = volumeTexts; sensitivityLabels = sensitivityTexts;
            statusText = status; suspicionText = suspicion; suspicionFill = suspicionBar; watchedText = watched;
            meetingPanel = meetingRoot; meetingText = meeting; promptPanel = promptRoot;
            promptText = prompt; promptFill = promptBar;
            bottomPanel = bottomRoot; taskTitle = title; taskSequence = sequence; taskHint = hint;
            endTitle = endTitleText; endStats = endStatsText;
        }

        void Start()
        {
            hostButton.onClick.AddListener(StartHost);
            joinButton.onClick.AddListener(StartClient);
            resumeButton.onClick.AddListener(() => SetPaused(false));
            leaveButton.onClick.AddListener(Disconnect);
            endMenuButton.onClick.AddListener(Disconnect);

            foreach (var slider in volumeSliders)
            {
                slider.SetValueWithoutNotify(AudioDirector.MasterVolume);
                slider.onValueChanged.AddListener(v => AudioDirector.MasterVolume = v);
            }
            foreach (var slider in sensitivitySliders)
            {
                slider.SetValueWithoutNotify(ThirdPersonCamera.Sensitivity);
                slider.onValueChanged.AddListener(v => ThirdPersonCamera.Sensitivity = v);
            }

            IsPaused = false;
        }

        void StartHost()
        {
            ApplyTransport();
            NetworkManager.Singleton.StartHost();
        }

        void StartClient()
        {
            ApplyTransport();
            NetworkManager.Singleton.StartClient();
        }

        void ApplyTransport()
        {
            var manager = NetworkManager.Singleton;
            var transport = manager != null ? manager.GetComponent<UnityTransport>() : null;
            if (transport != null) transport.SetConnectionData(addressField.text.Trim(), 7777);
        }

        void Disconnect()
        {
            SetPaused(false);
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }

        void SetPaused(bool paused)
        {
            IsPaused = paused;
            if (paused) CursorLock.Unlock();
            else CursorLock.Lock();
        }

        void Update()
        {
            var manager = NetworkManager.Singleton;
            bool connected = manager != null && (manager.IsClient || manager.IsServer);

            var status = PlayerStatus.Local;
            var game = GameManager.Instance;
            bool fired = status != null && status.IsCaught;
            bool survived = !fired && game != null && game.DayOver.Value;
            bool finished = connected && (fired || survived);

            if (!connected && IsPaused) SetPaused(false);
            if (connected && !finished && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetPaused(!IsPaused);
            }

            if (!connected && CursorLock.IsLocked) CursorLock.Unlock();

            mainMenu.SetActive(!connected);
            hud.SetActive(connected && !finished);
            pauseMenu.SetActive(connected && IsPaused && !finished);
            endScreen.SetActive(finished);

            SyncSettings();

            if (finished) UpdateEndScreen(fired);
            else if (connected) UpdateHud(manager, status, game);

            DriveAudioFeedback(status);
        }

        void SyncSettings()
        {
            foreach (var slider in volumeSliders)
            {
                if (!Mathf.Approximately(slider.value, AudioDirector.MasterVolume)) slider.SetValueWithoutNotify(AudioDirector.MasterVolume);
            }
            foreach (var label in volumeLabels)
            {
                label.text = $"Lautstärke   {Mathf.RoundToInt(AudioDirector.MasterVolume * 100f)}%";
            }

            foreach (var slider in sensitivitySliders)
            {
                if (!Mathf.Approximately(slider.value, ThirdPersonCamera.Sensitivity)) slider.SetValueWithoutNotify(ThirdPersonCamera.Sensitivity);
            }
            foreach (var label in sensitivityLabels)
            {
                label.text = $"Maus-Empfindlichkeit   {ThirdPersonCamera.Sensitivity:0.00}";
            }
        }

        void UpdateHud(NetworkManager manager, PlayerStatus status, GameManager game)
        {
            string role = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
            string clock = game != null ? FormatTime(game.TimeRemaining.Value) : "--:--";
            statusText.text = $"{role}   ·   Feierabend in {clock}";

            if (status != null)
            {
                float normalized = status.SuspicionNormalized;
                suspicionText.text = $"Verdacht   {Mathf.RoundToInt(status.Suspicion.Value)}%";
                suspicionFill.fillAmount = normalized;
                suspicionFill.color = Color.Lerp(Good, Bad, normalized);

                bool watched = status.IsWatched && !status.IsCaught;
                watchedText.gameObject.SetActive(watched);
                if (watched)
                {
                    Color color = watchedText.color;
                    color.a = 0.65f + Mathf.PingPong(Time.time * 1.6f, 0.35f);
                    watchedText.color = color;
                }
            }

            UpdateMeeting();
            UpdateBottom(status);
        }

        void UpdateMeeting()
        {
            var meeting = MeetingSystem.Instance;
            bool active = meeting != null && meeting.CurrentPhase != MeetingSystem.Phase.None;

            meetingPanel.SetActive(active);
            if (!active)
            {
                promptPanel.SetActive(false);
                return;
            }

            bool announced = meeting.CurrentPhase == MeetingSystem.Phase.Announced;
            meetingText.text = announced
                ? $"MEETING in {Mathf.CeilToInt(meeting.Countdown)}s   ·   Besprechungsraum (Ost)"
                : $"MEETING läuft   ·   noch {Mathf.CeilToInt(meeting.Countdown)}s";
            meetingText.color = announced ? new Color(0.98f, 0.82f, 0.38f) : new Color(0.62f, 0.84f, 0.98f);

            bool hasPrompt = meeting.CurrentPrompt != MeetingSystem.PromptAction.None;
            promptPanel.SetActive(hasPrompt);
            if (!hasPrompt) return;

            promptText.text = MeetingSystem.DescribePrompt(meeting.CurrentPrompt);
            promptFill.fillAmount = Mathf.Clamp01(meeting.PromptCountdown / 3f);
        }

        void UpdateBottom(PlayerStatus status)
        {
            var player = PlayerController.Local;
            if (player == null || status == null || status.IsCaught)
            {
                bottomPanel.SetActive(false);
                return;
            }

            WorkTaskRunner task = player.Task;
            if (task != null && task.IsActive)
            {
                bottomPanel.SetActive(true);
                taskTitle.text = $"{task.TaskName}   ·   erledigt: {task.Completed}";
                taskSequence.text = FormatSequence(task);
                taskHint.text = "[E] aufhören";
                return;
            }

            string message = null;
            if (player.NearbyStation != null) message = $"[E]   an {player.NearbyStation.Label} so tun als ob";
            else if (status.IsSeated) message = "Du sitzt im Meeting. Reagiere auf die Aufforderungen.";

            bottomPanel.SetActive(message != null);
            if (message == null) return;

            taskTitle.text = string.Empty;
            taskSequence.text = message;
            taskHint.text = string.Empty;
        }

        string FormatSequence(WorkTaskRunner task)
        {
            string[] keys = task.Sequence.Split(' ');
            _builder.Clear();

            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) _builder.Append("   ");
                if (i < task.Index) _builder.Append("<color=#3F7F55>·</color>");
                else _builder.Append(keys[i]);
            }

            return _builder.ToString();
        }

        void UpdateEndScreen(bool fired)
        {
            endTitle.text = fired ? "ERWISCHT — DU BIST GEFEUERT" : "FEIERABEND — NIEMAND HAT ETWAS GEMERKT";
            endTitle.color = fired ? Bad : Good;

            var player = PlayerController.Local;
            int done = player != null && player.Task != null ? player.Task.Completed : 0;
            endStats.text = $"Vorgetäuschte Aufgaben: {done}";
        }

        void DriveAudioFeedback(PlayerStatus status)
        {
            if (status != null)
            {
                if (status.IsWatched && !_wasWatched) AudioDirector.Alert();
                _wasWatched = status.IsWatched;

                if (status.IsCaught && !_wasCaught) AudioDirector.Fired();
                _wasCaught = status.IsCaught;
            }

            var meeting = MeetingSystem.Instance;
            if (meeting == null) return;

            if (meeting.CurrentPhase != _lastPhase && meeting.CurrentPhase == MeetingSystem.Phase.Announced)
            {
                AudioDirector.Chime();
            }
            _lastPhase = meeting.CurrentPhase;
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }
    }
}
