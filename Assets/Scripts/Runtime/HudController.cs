using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OfficeImposter
{
    // Drives the generated uGUI hierarchy. HudBuilder assigns these references when the
    // scene is generated, so nothing here searches by name at runtime.
    public class HudController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        [Header("Screens")]
        public GameObject mainMenu;
        public GameObject hud;
        public GameObject pauseMenu;
        public GameObject endScreen;

        [Header("Menu")]
        public TMP_InputField addressField;
        public Button hostButton;
        public Button joinButton;
        public Button resumeButton;
        public Button leaveButton;
        public Button endMenuButton;
        public Slider[] volumeSliders;
        public Slider[] sensitivitySliders;
        public TextMeshProUGUI[] volumeLabels;
        public TextMeshProUGUI[] sensitivityLabels;

        [Header("Status")]
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI suspicionText;
        public Image suspicionFill;
        public TextMeshProUGUI energyText;
        public Image energyFill;
        public TextMeshProUGUI reasonText;
        public TextMeshProUGUI watchedText;
        public GameObject crosshair;

        [Header("Meeting")]
        public GameObject meetingPanel;
        public TextMeshProUGUI meetingText;
        public GameObject promptPanel;
        public TextMeshProUGUI promptText;
        public Image promptFill;

        [Header("Job")]
        public GameObject jobPanel;
        public TextMeshProUGUI jobText;
        public TextMeshProUGUI jobTimerText;
        public Image jobFill;

        [Header("Confrontation")]
        public GameObject confrontPanel;
        public TextMeshProUGUI confrontQuestion;
        public TextMeshProUGUI[] confrontAnswers;
        public Image confrontFill;

        [Header("Task")]
        public GameObject bottomPanel;
        public TextMeshProUGUI taskTitle;
        public TextMeshProUGUI taskSequence;
        public TextMeshProUGUI taskHint;
        public GameObject holdPanel;
        public Image holdFill;
        public RectTransform holdWindow;

        [Header("End")]
        public TextMeshProUGUI endTitle;
        public TextMeshProUGUI endStats;
        public TextMeshProUGUI endGrade;

        static readonly Color Good = new Color(0.36f, 0.80f, 0.47f);
        static readonly Color Bad = new Color(0.93f, 0.26f, 0.26f);
        static readonly Color Warn = new Color(0.97f, 0.76f, 0.33f);

        readonly StringBuilder _builder = new StringBuilder();
        bool _wasWatched;
        bool _wasCaught;
        bool _wasConfronted;
        MeetingSystem.Phase _lastPhase = MeetingSystem.Phase.None;

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
                slider.SetValueWithoutNotify(FirstPersonLook.Sensitivity);
                slider.onValueChanged.AddListener(v => FirstPersonLook.Sensitivity = v);
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

            if (finished) UpdateEndScreen(fired, status);
            else if (connected) UpdateHud(manager, status, game);

            DriveAudioFeedback(status);
        }

        void SyncSettings()
        {
            foreach (var slider in volumeSliders)
            {
                if (!Mathf.Approximately(slider.value, AudioDirector.MasterVolume)) slider.SetValueWithoutNotify(AudioDirector.MasterVolume);
            }
            foreach (var label in volumeLabels) label.text = $"Lautstärke   {Mathf.RoundToInt(AudioDirector.MasterVolume * 100f)}%";

            foreach (var slider in sensitivitySliders)
            {
                if (!Mathf.Approximately(slider.value, FirstPersonLook.Sensitivity)) slider.SetValueWithoutNotify(FirstPersonLook.Sensitivity);
            }
            foreach (var label in sensitivityLabels) label.text = $"Maus-Empfindlichkeit   {FirstPersonLook.Sensitivity:0.00}";
        }

        void UpdateHud(NetworkManager manager, PlayerStatus status, GameManager game)
        {
            string role = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
            string clock = game != null ? FormatTime(game.TimeRemaining.Value) : "--:--";

            if (game != null && game.InGrace)
            {
                statusText.text = $"Einarbeitungszeit — noch {Mathf.CeilToInt(game.Grace.Value)}s bis es zählt";
                statusText.color = new Color(0.55f, 0.85f, 0.62f);
            }
            else
            {
                statusText.text = $"{role}   ·   Feierabend in {clock}";
                statusText.color = new Color(0.91f, 0.93f, 0.96f);
            }

            if (status != null)
            {
                float suspicion = status.SuspicionNormalized;
                suspicionText.text = $"Verdacht   {Mathf.RoundToInt(status.Suspicion.Value)}%";
                suspicionFill.fillAmount = suspicion;
                suspicionFill.color = Color.Lerp(Good, Bad, suspicion);

                float energy = status.EnergyNormalized;
                energyText.text = status.IsExhausted
                    ? $"Energie   {Mathf.RoundToInt(status.Energy.Value)}%   ·   übermüdet"
                    : $"Energie   {Mathf.RoundToInt(status.Energy.Value)}%";
                energyFill.fillAmount = energy;
                energyFill.color = Color.Lerp(Bad, new Color(0.45f, 0.72f, 0.95f), energy);

                string reason = PlayerStatus.DescribeReason(status.LastReason);
                reasonText.gameObject.SetActive(!string.IsNullOrEmpty(reason));
                reasonText.text = reason;

                bool watched = status.IsWatched && !status.IsCaught;
                watchedText.gameObject.SetActive(watched);
                if (watched)
                {
                    Color color = watchedText.color;
                    color.a = 0.65f + Mathf.PingPong(Time.time * 1.6f, 0.35f);
                    watchedText.color = color;
                }
            }

            UpdateJob(status);
            UpdateConfrontation(status);
            UpdateMeeting();
            UpdateBottom(status);

            var player = PlayerController.Local;
            bool showCrosshair = player != null && status != null && !status.IsCaught && !status.IsConfronted
                                 && (player.Task == null || !player.Task.IsActive);
            crosshair.SetActive(showCrosshair);
        }

        void UpdateJob(PlayerStatus status)
        {
            bool has = status != null && status.HasJob;
            jobPanel.SetActive(has);
            if (!has) return;

            var kind = (JobKind)status.JobKind.Value;
            jobText.text = JobText.Describe(kind, status.JobStage.Value);

            float remaining = Mathf.Max(0f, status.JobDeadline.Value);
            jobTimerText.text = $"{Mathf.CeilToInt(remaining)}s";
            jobTimerText.color = remaining < 15f ? Bad : Warn;

            jobFill.fillAmount = Mathf.Clamp01(remaining / 55f);
            jobFill.color = remaining < 15f ? Bad : Warn;
        }

        void UpdateConfrontation(PlayerStatus status)
        {
            bool active = status != null && status.IsConfronted;
            confrontPanel.SetActive(active);
            if (!active) return;

            var question = ConfrontationBank.Get(status.ConfrontQuestion.Value);
            confrontQuestion.text = $"„{question.Text}“";

            for (int i = 0; i < confrontAnswers.Length; i++)
            {
                confrontAnswers[i].text = i < question.Answers.Length
                    ? $"[{i + 1}]   {question.Answers[i]}"
                    : string.Empty;
            }

            confrontFill.fillAmount = Mathf.Clamp01(status.ConfrontTimer.Value / 9f);
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
            meetingText.color = announced ? Warn : new Color(0.62f, 0.84f, 0.98f);

            bool hasPrompt = meeting.CurrentPrompt != MeetingSystem.PromptAction.None;
            promptPanel.SetActive(hasPrompt);
            if (!hasPrompt) return;

            promptText.text = MeetingSystem.DescribePrompt(meeting.CurrentPrompt);
            promptFill.fillAmount = Mathf.Clamp01(meeting.PromptCountdown / 3f);
        }

        void UpdateBottom(PlayerStatus status)
        {
            var player = PlayerController.Local;
            if (player == null || status == null || status.IsCaught || status.IsConfronted)
            {
                bottomPanel.SetActive(false);
                holdPanel.SetActive(false);
                return;
            }

            WorkTaskRunner task = player.Task;
            if (task != null && task.IsActive)
            {
                bottomPanel.SetActive(true);
                taskTitle.text = $"{task.TaskName}   ·   erledigt: {task.Completed}";

                if (task.Kind == WorkTaskKind.HoldRelease)
                {
                    taskSequence.text = "[LEERTASTE] halten, im grünen Bereich loslassen";
                    taskHint.text = "[E] aufhören";
                    holdPanel.SetActive(true);
                    holdFill.fillAmount = task.HoldValue;

                    holdWindow.anchorMin = new Vector2(task.HoldMin, 0f);
                    holdWindow.anchorMax = new Vector2(task.HoldMax, 1f);
                    holdWindow.offsetMin = Vector2.zero;
                    holdWindow.offsetMax = Vector2.zero;
                }
                else
                {
                    taskSequence.text = FormatSequence(task);
                    taskHint.text = "[E] aufhören";
                    holdPanel.SetActive(false);
                }
                return;
            }

            holdPanel.SetActive(false);

            string message = null;
            if (player.Focus != null) message = player.Focus.Prompt;
            else if (player.IsSeated) message = "[E]   aufstehen";
            else if (status.IsSeated) message = "Du sitzt im Meeting. Reagiere auf die Aufforderungen.";
            else if (status.IsCarrying) message = "Du trägst etwas — bring es zur Ablage des Chefs.";

            bottomPanel.SetActive(message != null);
            if (message == null) return;

            taskTitle.text = string.Empty;
            taskSequence.text = message;
            taskHint.text = player.IsCrouching ? "[STRG] aufrichten" : string.Empty;
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

        void UpdateEndScreen(bool fired, PlayerStatus status)
        {
            endTitle.text = fired ? "ERWISCHT — DU BIST GEFEUERT" : "FEIERABEND — NIEMAND HAT ETWAS GEMERKT";
            endTitle.color = fired ? Bad : Good;

            int tasks = status != null ? status.TasksDone.Value : 0;
            int jobs = status != null ? status.JobsDone.Value : 0;
            int missed = status != null ? status.JobsMissed.Value : 0;

            endStats.text = $"Vorgetäuschte Aufgaben: {tasks}     ·     Aufträge erledigt: {jobs}     ·     verpasst: {missed}";
            endGrade.text = Grade(fired, tasks, jobs, missed);
        }

        static string Grade(bool fired, int tasks, int jobs, int missed)
        {
            if (fired) return "Mitarbeitergespräch:  Note 6 — fristlos";

            float score = jobs * 2f + tasks * 0.5f - missed * 2f;
            if (score >= 12f) return "Mitarbeitergespräch:  Note 1 — „Vorbildlich. Wir befördern Sie.“";
            if (score >= 8f) return "Mitarbeitergespräch:  Note 2 — „Sehr solide Arbeit.“";
            if (score >= 4f) return "Mitarbeitergespräch:  Note 3 — „Geht in Ordnung.“";
            if (score >= 1f) return "Mitarbeitergespräch:  Note 4 — „Da geht noch mehr.“";
            return "Mitarbeitergespräch:  Note 5 — „Wir müssen reden.“";
        }

        void DriveAudioFeedback(PlayerStatus status)
        {
            if (status != null)
            {
                if (status.IsWatched && !_wasWatched) AudioDirector.Alert();
                _wasWatched = status.IsWatched;

                if (status.IsCaught && !_wasCaught) AudioDirector.Fired();
                _wasCaught = status.IsCaught;

                if (status.IsConfronted && !_wasConfronted) AudioDirector.Alert();
                _wasConfronted = status.IsConfronted;
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
