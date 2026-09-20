using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    using OfficeImposter;

    // Builds the uGUI screens in code so the interface is reproducible from source
    // alongside the rest of the scene.
    public static class HudBuilder
    {
        static TMP_FontAsset _font;

        static readonly Color Ink = new Color(0.91f, 0.93f, 0.96f);
        static readonly Color Muted = new Color(0.62f, 0.66f, 0.74f);
        static readonly Color Card = new Color(0.07f, 0.08f, 0.11f, 0.94f);
        static readonly Color Scrim = new Color(0.02f, 0.03f, 0.05f, 0.80f);
        static readonly Color Slot = new Color(0.16f, 0.18f, 0.23f, 0.95f);
        static readonly Color Accent = new Color(0.30f, 0.55f, 0.85f);
        static readonly Color Track = new Color(0.12f, 0.13f, 0.17f, 1f);

        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpSetup.FontAssetPath);
            if (_font == null) Debug.LogWarning("[HudBuilder] Roboto SDF font missing; TMP will fall back to its default.");

            CreateEventSystem();

            var canvasGo = new GameObject("UI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            Transform root = canvasGo.transform;
            var hud = canvasGo.AddComponent<HudController>();

            BuildMainMenu(root, hud);
            BuildHud(root, hud);
            BuildPauseMenu(root, hud);
            BuildEndScreen(root, hud);
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // The project uses the Input System package, so the legacy module would not work.
            go.AddComponent<InputSystemUIInputModule>();
        }

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(string name, Transform parent)
        {
            RectTransform rt = Node(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Place(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rt = Node(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Fill(RectTransform rt, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string content, float size,
            TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = Stretch(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null) text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.enableWordWrapping = true;

            return text;
        }

        static TextMeshProUGUI LabelAt(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            string content, float fontSize, TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
        {
            return Label(name, Place(name + "Rect", parent, anchor, position, size), content, fontSize, align, color, style);
        }

        // A dark track with a horizontally filled bar on top.
        static Image Bar(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform track = Place(name, parent, anchor, position, size);
            Fill(track, Track);

            Image fill = Fill(Stretch("Fill", track), color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            return fill;
        }

        static Button MakeButton(string name, Transform parent, string caption, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rt = Place(name, parent, anchor, position, size);
            Image image = Fill(rt, Slot);
            image.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Slot;
            colors.highlightedColor = new Color(0.24f, 0.28f, 0.36f, 1f);
            colors.pressedColor = Accent;
            colors.selectedColor = Slot;
            button.colors = colors;

            Label("Label", rt, caption, 22f, TextAlignmentOptions.Center, Ink);
            return button;
        }

        static Slider MakeSlider(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            float min, float max, float value)
        {
            RectTransform rt = Place(name, parent, anchor, position, size);
            var slider = rt.gameObject.AddComponent<Slider>();

            Image background = Fill(Stretch("Background", rt), new Color(0.13f, 0.15f, 0.19f, 1f));
            background.raycastTarget = true;

            RectTransform fillArea = Stretch("Fill Area", rt);
            RectTransform fillRect = Stretch("Fill", fillArea);
            Fill(fillRect, Accent);

            RectTransform handleArea = Stretch("Handle Slide Area", rt);
            RectTransform handle = Node("Handle", handleArea);
            handle.sizeDelta = new Vector2(16f, 0f);
            Image handleImage = Fill(handle, Ink);
            handleImage.raycastTarget = true;

            slider.fillRect = fillRect;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            return slider;
        }

        static void BuildSettings(Transform parent, float y, out Slider volume, out Slider sensitivity,
            out TextMeshProUGUI volumeLabel, out TextMeshProUGUI sensitivityLabel)
        {
            var anchor = new Vector2(0.5f, 1f);

            volumeLabel = LabelAt("VolumeLabel", parent, anchor, new Vector2(0f, y), new Vector2(420f, 24f),
                "Lautstärke", 17f, TextAlignmentOptions.Left, Muted);
            volume = MakeSlider("VolumeSlider", parent, anchor, new Vector2(0f, y - 26f), new Vector2(420f, 14f), 0f, 1f, 0.7f);

            sensitivityLabel = LabelAt("SensitivityLabel", parent, anchor, new Vector2(0f, y - 56f), new Vector2(420f, 24f),
                "Maus-Empfindlichkeit", 17f, TextAlignmentOptions.Left, Muted);
            sensitivity = MakeSlider("SensitivitySlider", parent, anchor, new Vector2(0f, y - 82f), new Vector2(420f, 14f), 0.03f, 0.35f, 0.12f);
        }

        static void BuildMainMenu(Transform root, HudController hud)
        {
            RectTransform rt = Stretch("MainMenu", root);
            hud.mainMenu = rt.gameObject;
            Fill(rt, Scrim).raycastTarget = true;

            RectTransform card = Place("Card", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 620f));
            Fill(card, Card);

            var top = new Vector2(0.5f, 1f);
            LabelAt("Title", card, top, new Vector2(0f, -34f), new Vector2(540f, 56f),
                "OFFICE IMPOSTER", 46f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            LabelAt("Subtitle", card, top, new Vector2(0f, -94f), new Vector2(540f, 60f),
                "Du hast keine Ahnung, was dein Job ist.\nÜberlebe den Arbeitstag, ohne dass es jemand merkt.",
                17f, TextAlignmentOptions.Center, Muted);

            hud.hostButton = MakeButton("HostButton", card, "Arbeitstag hosten", top, new Vector2(0f, -172f), new Vector2(460f, 54f));

            LabelAt("AddressLabel", card, top, new Vector2(0f, -238f), new Vector2(460f, 22f),
                "Server-Adresse", 16f, TextAlignmentOptions.Left, Muted);
            hud.addressField = MakeInputField("AddressField", card, "127.0.0.1", top, new Vector2(0f, -262f), new Vector2(460f, 38f));

            hud.joinButton = MakeButton("JoinButton", card, "Beitreten", top, new Vector2(0f, -310f), new Vector2(460f, 46f));

            LabelAt("Controls", card, top, new Vector2(0f, -368f), new Vector2(540f, 60f),
                "WASD laufen · Maus umsehen · Shift rennen (laut!) · Strg ducken\nE interagieren · 1/2/3 antworten · Esc Pause",
                15f, TextAlignmentOptions.Center, Muted);

            BuildSettings(card, -436f, out Slider volume, out Slider sensitivity,
                out TextMeshProUGUI volumeLabel, out TextMeshProUGUI sensitivityLabel);

            hud.volumeSliders = new[] { volume };
            hud.sensitivitySliders = new[] { sensitivity };
            hud.volumeLabels = new[] { volumeLabel };
            hud.sensitivityLabels = new[] { sensitivityLabel };
        }

        static TMP_InputField MakeInputField(string name, Transform parent, string initial,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rt = Place(name, parent, anchor, position, size);
            Image background = Fill(rt, Slot);
            background.raycastTarget = true;

            var field = rt.gameObject.AddComponent<TMP_InputField>();

            RectTransform viewport = Stretch("Text Area", rt);
            viewport.offsetMin = new Vector2(12f, 4f);
            viewport.offsetMax = new Vector2(-12f, -4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = Label("Placeholder", viewport, "IP-Adresse", 18f, TextAlignmentOptions.Left, Muted);
            TextMeshProUGUI text = Label("Text", viewport, string.Empty, 18f, TextAlignmentOptions.Left, Ink);
            text.raycastTarget = true;

            field.targetGraphic = background;
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = initial;

            return field;
        }

        static void BuildHud(Transform root, HudController hud)
        {
            RectTransform rt = Stretch("Hud", root);
            hud.hud = rt.gameObject;

            BuildStatusPanel(rt, hud);
            BuildCrosshair(rt, hud);
            BuildMeetingPanels(rt, hud);
            BuildJobPanel(rt, hud);
            BuildConfrontPanel(rt, hud);
            BuildTaskPanel(rt, hud);

            var top = new Vector2(0.5f, 1f);
            hud.watchedText = LabelAt("WatchedText", rt, top, new Vector2(0f, -196f), new Vector2(700f, 40f),
                "DER CHEF SIEHT DICH", 30f, TextAlignmentOptions.Center, new Color(0.97f, 0.42f, 0.35f), FontStyles.Bold);
            hud.watchedText.gameObject.SetActive(false);
        }

        static void BuildStatusPanel(Transform parent, HudController hud)
        {
            var topLeft = new Vector2(0f, 1f);
            RectTransform panel = Place("StatusPanel", parent, topLeft, new Vector2(26f, -26f), new Vector2(400f, 168f));
            Fill(panel, Card);

            hud.statusText = LabelAt("StatusText", panel, topLeft, new Vector2(20f, -14f), new Vector2(360f, 26f),
                "", 19f, TextAlignmentOptions.Left, Ink);

            hud.suspicionText = LabelAt("SuspicionText", panel, topLeft, new Vector2(20f, -46f), new Vector2(360f, 24f),
                "", 18f, TextAlignmentOptions.Left, Ink);
            hud.suspicionFill = Bar("SuspicionBar", panel, topLeft, new Vector2(20f, -76f), new Vector2(360f, 18f), Color.white);

            hud.energyText = LabelAt("EnergyText", panel, topLeft, new Vector2(20f, -100f), new Vector2(360f, 24f),
                "", 17f, TextAlignmentOptions.Left, Muted);
            hud.energyFill = Bar("EnergyBar", panel, topLeft, new Vector2(20f, -128f), new Vector2(360f, 12f), Accent);

            hud.reasonText = LabelAt("ReasonText", parent, topLeft, new Vector2(26f, -202f), new Vector2(520f, 26f),
                "", 17f, TextAlignmentOptions.Left, new Color(0.97f, 0.62f, 0.45f));
            hud.reasonText.gameObject.SetActive(false);
        }

        static void BuildCrosshair(Transform parent, HudController hud)
        {
            RectTransform dot = Place("Crosshair", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            Fill(dot, new Color(1f, 1f, 1f, 0.75f));
            hud.crosshair = dot.gameObject;
        }

        static void BuildMeetingPanels(Transform parent, HudController hud)
        {
            var top = new Vector2(0.5f, 1f);

            RectTransform meeting = Place("MeetingPanel", parent, top, new Vector2(0f, -26f), new Vector2(680f, 58f));
            hud.meetingPanel = meeting.gameObject;
            Fill(meeting, Card);
            hud.meetingText = Label("MeetingText", Stretch("MeetingTextRect", meeting), "", 21f,
                TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            RectTransform prompt = Place("PromptPanel", parent, top, new Vector2(0f, -92f), new Vector2(680f, 88f));
            hud.promptPanel = prompt.gameObject;
            Fill(prompt, Card);
            hud.promptText = LabelAt("PromptText", prompt, top, new Vector2(0f, -12f), new Vector2(640f, 46f),
                "", 34f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            hud.promptFill = Bar("PromptBar", prompt, top, new Vector2(0f, -68f), new Vector2(620f, 8f), Accent);

            hud.meetingPanel.SetActive(false);
            hud.promptPanel.SetActive(false);
        }

        static void BuildJobPanel(Transform parent, HudController hud)
        {
            var topRight = new Vector2(1f, 1f);
            RectTransform panel = Place("JobPanel", parent, topRight, new Vector2(-26f, -26f), new Vector2(420f, 108f));
            hud.jobPanel = panel.gameObject;
            Fill(panel, Card);

            LabelAt("JobHeading", panel, topRight, new Vector2(-20f, -14f), new Vector2(380f, 22f),
                "AUFTRAG VOM CHEF", 15f, TextAlignmentOptions.Right, Muted, FontStyles.Bold);

            hud.jobText = LabelAt("JobText", panel, topRight, new Vector2(-20f, -40f), new Vector2(380f, 26f),
                "", 19f, TextAlignmentOptions.Right, Ink);

            hud.jobTimerText = LabelAt("JobTimer", panel, topRight, new Vector2(-20f, -68f), new Vector2(380f, 22f),
                "", 17f, TextAlignmentOptions.Right, Ink);

            hud.jobFill = Bar("JobBar", panel, topRight, new Vector2(-20f, -92f), new Vector2(380f, 8f), Color.white);
            hud.jobPanel.SetActive(false);
        }

        static void BuildConfrontPanel(Transform parent, HudController hud)
        {
            RectTransform panel = Place("ConfrontPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(860f, 300f));
            hud.confrontPanel = panel.gameObject;
            Fill(panel, Card);

            var top = new Vector2(0.5f, 1f);
            LabelAt("ConfrontHeading", panel, top, new Vector2(0f, -16f), new Vector2(800f, 24f),
                "DER CHEF STEHT VOR DIR", 16f, TextAlignmentOptions.Center, new Color(0.97f, 0.42f, 0.35f), FontStyles.Bold);

            hud.confrontQuestion = LabelAt("ConfrontQuestion", panel, top, new Vector2(0f, -46f), new Vector2(800f, 60f),
                "", 26f, TextAlignmentOptions.Center, Ink, FontStyles.Italic);

            var answers = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                answers[i] = LabelAt($"Answer{i + 1}", panel, top, new Vector2(0f, -120f - i * 42f), new Vector2(780f, 36f),
                    "", 20f, TextAlignmentOptions.Left, Ink);
            }
            hud.confrontAnswers = answers;

            hud.confrontFill = Bar("ConfrontBar", panel, top, new Vector2(0f, -262f), new Vector2(780f, 10f), new Color(0.95f, 0.45f, 0.35f));
            hud.confrontPanel.SetActive(false);
        }

        static void BuildTaskPanel(Transform parent, HudController hud)
        {
            var bottom = new Vector2(0.5f, 0f);

            RectTransform panel = Place("BottomPanel", parent, bottom, new Vector2(0f, 46f), new Vector2(820f, 140f));
            hud.bottomPanel = panel.gameObject;
            Fill(panel, Card);

            var top = new Vector2(0.5f, 1f);
            hud.taskTitle = LabelAt("TaskTitle", panel, top, new Vector2(0f, -12f), new Vector2(760f, 26f),
                "", 19f, TextAlignmentOptions.Center, Muted);
            hud.taskSequence = LabelAt("TaskSequence", panel, top, new Vector2(0f, -44f), new Vector2(760f, 54f),
                "", 36f, TextAlignmentOptions.Center, new Color(0.62f, 0.86f, 1f), FontStyles.Bold);
            hud.taskHint = LabelAt("TaskHint", panel, top, new Vector2(0f, -104f), new Vector2(760f, 24f),
                "", 17f, TextAlignmentOptions.Center, Muted);

            // Timing minigame: a track, a green release window, and the filling needle.
            RectTransform hold = Place("HoldPanel", parent, bottom, new Vector2(0f, 200f), new Vector2(640f, 34f));
            hud.holdPanel = hold.gameObject;
            Fill(hold, Track);

            RectTransform window = Node("Window", hold);
            window.anchorMin = new Vector2(0.55f, 0f);
            window.anchorMax = new Vector2(0.72f, 1f);
            window.offsetMin = Vector2.zero;
            window.offsetMax = Vector2.zero;
            Fill(window, new Color(0.32f, 0.78f, 0.45f, 0.85f));
            hud.holdWindow = window;

            Image fill = Fill(Stretch("Needle", hold), new Color(1f, 1f, 1f, 0.55f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            hud.holdFill = fill;

            hud.bottomPanel.SetActive(false);
            hud.holdPanel.SetActive(false);
        }

        static void BuildPauseMenu(Transform root, HudController hud)
        {
            RectTransform rt = Stretch("PauseMenu", root);
            hud.pauseMenu = rt.gameObject;
            Fill(rt, Scrim).raycastTarget = true;

            RectTransform card = Place("Card", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 420f));
            Fill(card, Card);

            var top = new Vector2(0.5f, 1f);
            LabelAt("Title", card, top, new Vector2(0f, -30f), new Vector2(440f, 48f),
                "Pause", 38f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            hud.resumeButton = MakeButton("ResumeButton", card, "Weiter", top, new Vector2(0f, -96f), new Vector2(420f, 48f));
            BuildSettings(card, -166f, out Slider volume, out Slider sensitivity,
                out TextMeshProUGUI volumeLabel, out TextMeshProUGUI sensitivityLabel);
            hud.leaveButton = MakeButton("LeaveButton", card, "Arbeitstag verlassen", top, new Vector2(0f, -310f), new Vector2(420f, 46f));

            hud.volumeSliders = Append(hud.volumeSliders, volume);
            hud.sensitivitySliders = Append(hud.sensitivitySliders, sensitivity);
            hud.volumeLabels = Append(hud.volumeLabels, volumeLabel);
            hud.sensitivityLabels = Append(hud.sensitivityLabels, sensitivityLabel);

            hud.pauseMenu.SetActive(false);
        }

        static T[] Append<T>(T[] source, T value)
        {
            if (source == null) return new[] { value };

            var result = new T[source.Length + 1];
            source.CopyTo(result, 0);
            result[source.Length] = value;
            return result;
        }

        static void BuildEndScreen(Transform root, HudController hud)
        {
            RectTransform rt = Stretch("EndScreen", root);
            hud.endScreen = rt.gameObject;
            Fill(rt, Scrim).raycastTarget = true;

            RectTransform card = Place("Card", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 360f));
            Fill(card, Card);

            var top = new Vector2(0.5f, 1f);
            hud.endTitle = LabelAt("EndTitle", card, top, new Vector2(0f, -50f), new Vector2(940f, 70f),
                "", 44f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            hud.endStats = LabelAt("EndStats", card, top, new Vector2(0f, -134f), new Vector2(940f, 32f),
                "", 21f, TextAlignmentOptions.Center, Muted);
            hud.endGrade = LabelAt("EndGrade", card, top, new Vector2(0f, -182f), new Vector2(940f, 34f),
                "", 23f, TextAlignmentOptions.Center, Ink);

            hud.endMenuButton = MakeButton("EndMenuButton", card, "Zurück zum Menü", top, new Vector2(0f, -250f), new Vector2(320f, 50f));
            hud.endScreen.SetActive(false);
        }
    }
}
