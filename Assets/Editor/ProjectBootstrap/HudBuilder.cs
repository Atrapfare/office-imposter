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
            var controller = canvasGo.AddComponent<HudController>();

            BuildMainMenu(root, out GameObject menu, out TMP_InputField address,
                out Button host, out Button join, out Slider menuVolume, out Slider menuSensitivity,
                out TextMeshProUGUI menuVolumeLabel, out TextMeshProUGUI menuSensitivityLabel);

            BuildHud(root, out GameObject hud, out TextMeshProUGUI status, out TextMeshProUGUI suspicion,
                out Image suspicionFill, out TextMeshProUGUI watched, out GameObject meetingPanel,
                out TextMeshProUGUI meetingText, out GameObject promptPanel, out TextMeshProUGUI promptText,
                out Image promptFill, out GameObject bottomPanel, out TextMeshProUGUI taskTitle,
                out TextMeshProUGUI taskSequence, out TextMeshProUGUI taskHint);

            BuildPauseMenu(root, out GameObject pause, out Button resume, out Button leave,
                out Slider pauseVolume, out Slider pauseSensitivity,
                out TextMeshProUGUI pauseVolumeLabel, out TextMeshProUGUI pauseSensitivityLabel);
            BuildEndScreen(root, out GameObject end, out TextMeshProUGUI endTitle,
                out TextMeshProUGUI endStats, out Button endMenu);

            controller.Bind(menu, hud, pause, end,
                address, host, join, resume, leave, endMenu,
                new[] { menuVolume, pauseVolume },
                new[] { menuSensitivity, pauseSensitivity },
                new[] { menuVolumeLabel, pauseVolumeLabel },
                new[] { menuSensitivityLabel, pauseSensitivityLabel },
                status, suspicion, suspicionFill, watched,
                meetingPanel, meetingText, promptPanel, promptText, promptFill,
                bottomPanel, taskTitle, taskSequence, taskHint,
                endTitle, endStats);
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

        static Button MakeButton(string name, Transform parent, string caption, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rt = Place(name, parent, anchor, position, size);
            Image image = Fill(rt, Slot);

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

            RectTransform background = Stretch("Background", rt);
            Fill(background, new Color(0.13f, 0.15f, 0.19f, 1f));

            RectTransform fillArea = Stretch("Fill Area", rt);
            fillArea.offsetMin = new Vector2(0f, 0f);
            fillArea.offsetMax = new Vector2(0f, 0f);

            RectTransform fillRect = Stretch("Fill", fillArea);
            Image fillImage = Fill(fillRect, Accent);

            RectTransform handleArea = Stretch("Handle Slide Area", rt);
            RectTransform handle = Node("Handle", handleArea);
            handle.sizeDelta = new Vector2(16f, 0f);
            Image handleImage = Fill(handle, Ink);

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

            volumeLabel = Label("VolumeLabel", Place("VolumeLabelRect", parent, anchor, new Vector2(0f, y), new Vector2(420f, 24f)),
                "Lautstärke", 17f, TextAlignmentOptions.Left, Muted);
            volume = MakeSlider("VolumeSlider", parent, anchor, new Vector2(0f, y - 26f), new Vector2(420f, 14f), 0f, 1f, 0.7f);

            sensitivityLabel = Label("SensitivityLabel", Place("SensitivityLabelRect", parent, anchor, new Vector2(0f, y - 56f), new Vector2(420f, 24f)),
                "Maus-Empfindlichkeit", 17f, TextAlignmentOptions.Left, Muted);
            sensitivity = MakeSlider("SensitivitySlider", parent, anchor, new Vector2(0f, y - 82f), new Vector2(420f, 14f), 0.03f, 0.35f, 0.12f);
        }

        static void BuildMainMenu(Transform root, out GameObject menu, out TMP_InputField address,
            out Button host, out Button join, out Slider volume, out Slider sensitivity,
            out TextMeshProUGUI volumeLabel, out TextMeshProUGUI sensitivityLabel)
        {
            RectTransform rt = Stretch("MainMenu", root);
            menu = rt.gameObject;
            Fill(rt, Scrim);

            RectTransform card = Place("Card", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 560f));
            Fill(card, Card);

            var top = new Vector2(0.5f, 1f);
            Label("Title", Place("TitleRect", card, top, new Vector2(0f, -34f), new Vector2(480f, 56f)),
                "OFFICE IMPOSTER", 46f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            Label("Subtitle", Place("SubtitleRect", card, top, new Vector2(0f, -92f), new Vector2(480f, 46f)),
                "Du hast keine Ahnung, was dein Job ist.\nLass es niemanden merken.", 17f, TextAlignmentOptions.Center, Muted);

            host = MakeButton("HostButton", card, "Arbeitstag hosten", top, new Vector2(0f, -156f), new Vector2(420f, 54f));

            Label("AddressLabel", Place("AddressLabelRect", card, top, new Vector2(0f, -222f), new Vector2(420f, 22f)),
                "Server-Adresse", 16f, TextAlignmentOptions.Left, Muted);
            address = MakeInputField("AddressField", card, "127.0.0.1", top, new Vector2(0f, -246f), new Vector2(420f, 38f));

            join = MakeButton("JoinButton", card, "Beitreten", top, new Vector2(0f, -294f), new Vector2(420f, 46f));

            BuildSettings(card, -366f, out volume, out sensitivity, out volumeLabel, out sensitivityLabel);
        }

        static TMP_InputField MakeInputField(string name, Transform parent, string initial,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rt = Place(name, parent, anchor, position, size);
            Image background = Fill(rt, Slot);

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

        static void BuildHud(Transform root, out GameObject hud, out TextMeshProUGUI status,
            out TextMeshProUGUI suspicion, out Image suspicionFill, out TextMeshProUGUI watched,
            out GameObject meetingPanel, out TextMeshProUGUI meetingText,
            out GameObject promptPanel, out TextMeshProUGUI promptText, out Image promptFill,
            out GameObject bottomPanel, out TextMeshProUGUI taskTitle,
            out TextMeshProUGUI taskSequence, out TextMeshProUGUI taskHint)
        {
            RectTransform rt = Stretch("Hud", root);
            hud = rt.gameObject;

            var topLeft = new Vector2(0f, 1f);
            RectTransform panel = Place("StatusPanel", rt, topLeft, new Vector2(26f, -26f), new Vector2(390f, 116f));
            Fill(panel, Card);

            status = Label("StatusText", Place("StatusTextRect", panel, topLeft, new Vector2(20f, -16f), new Vector2(350f, 26f)),
                "", 19f, TextAlignmentOptions.Left, Ink);
            suspicion = Label("SuspicionText", Place("SuspicionTextRect", panel, topLeft, new Vector2(20f, -48f), new Vector2(350f, 24f)),
                "", 18f, TextAlignmentOptions.Left, Ink);

            RectTransform barBackground = Place("SuspicionBar", panel, topLeft, new Vector2(20f, -82f), new Vector2(350f, 18f));
            Fill(barBackground, new Color(0.12f, 0.13f, 0.17f, 1f));

            RectTransform barFill = Stretch("Fill", barBackground);
            suspicionFill = Fill(barFill, Color.white);
            suspicionFill.type = Image.Type.Filled;
            suspicionFill.fillMethod = Image.FillMethod.Horizontal;
            suspicionFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            suspicionFill.fillAmount = 0f;

            var top = new Vector2(0.5f, 1f);
            watched = Label("WatchedText", Place("WatchedRect", rt, top, new Vector2(0f, -170f), new Vector2(700f, 40f)),
                "DER CHEF SIEHT DICH", 30f, TextAlignmentOptions.Center, new Color(0.97f, 0.42f, 0.35f), FontStyles.Bold);
            watched.gameObject.SetActive(false);

            RectTransform meeting = Place("MeetingPanel", rt, top, new Vector2(0f, -26f), new Vector2(660f, 60f));
            meetingPanel = meeting.gameObject;
            Fill(meeting, Card);
            meetingText = Label("MeetingText", Stretch("MeetingTextRect", meeting), "", 21f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            RectTransform prompt = Place("PromptPanel", rt, top, new Vector2(0f, -94f), new Vector2(660f, 84f));
            promptPanel = prompt.gameObject;
            Fill(prompt, Card);
            promptText = Label("PromptText", Place("PromptTextRect", prompt, top, new Vector2(0f, -12f), new Vector2(620f, 44f)),
                "", 34f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            RectTransform promptBar = Place("PromptBar", prompt, top, new Vector2(0f, -64f), new Vector2(600f, 8f));
            Fill(promptBar, new Color(0.12f, 0.13f, 0.17f, 1f));
            RectTransform promptBarFill = Stretch("Fill", promptBar);
            promptFill = Fill(promptBarFill, Accent);
            promptFill.type = Image.Type.Filled;
            promptFill.fillMethod = Image.FillMethod.Horizontal;
            promptFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            var bottom = new Vector2(0.5f, 0f);
            RectTransform bottomRect = Place("BottomPanel", rt, bottom, new Vector2(0f, 46f), new Vector2(760f, 140f));
            bottomPanel = bottomRect.gameObject;
            Fill(bottomRect, Card);

            var bottomTop = new Vector2(0.5f, 1f);
            taskTitle = Label("TaskTitle", Place("TaskTitleRect", bottomRect, bottomTop, new Vector2(0f, -12f), new Vector2(700f, 26f)),
                "", 19f, TextAlignmentOptions.Center, Muted);
            taskSequence = Label("TaskSequence", Place("TaskSequenceRect", bottomRect, bottomTop, new Vector2(0f, -44f), new Vector2(700f, 52f)),
                "", 40f, TextAlignmentOptions.Center, new Color(0.62f, 0.86f, 1f), FontStyles.Bold);
            taskHint = Label("TaskHint", Place("TaskHintRect", bottomRect, bottomTop, new Vector2(0f, -102f), new Vector2(700f, 24f)),
                "", 17f, TextAlignmentOptions.Center, Muted);

            meetingPanel.SetActive(false);
            promptPanel.SetActive(false);
            bottomPanel.SetActive(false);
        }

        static void BuildPauseMenu(Transform root, out GameObject pause, out Button resume, out Button leave,
            out Slider volume, out Slider sensitivity, out TextMeshProUGUI volumeLabel, out TextMeshProUGUI sensitivityLabel)
        {
            RectTransform rt = Stretch("PauseMenu", root);
            pause = rt.gameObject;
            Fill(rt, Scrim);

            RectTransform card = Place("Card", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 420f));
            Fill(card, Card);

            var top = new Vector2(0.5f, 1f);
            Label("Title", Place("TitleRect", card, top, new Vector2(0f, -30f), new Vector2(440f, 48f)),
                "Pause", 38f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            resume = MakeButton("ResumeButton", card, "Weiter", top, new Vector2(0f, -96f), new Vector2(420f, 48f));
            BuildSettings(card, -166f, out volume, out sensitivity, out volumeLabel, out sensitivityLabel);
            leave = MakeButton("LeaveButton", card, "Arbeitstag verlassen", top, new Vector2(0f, -310f), new Vector2(420f, 46f));

            pause.SetActive(false);
        }

        static void BuildEndScreen(Transform root, out GameObject end, out TextMeshProUGUI title,
            out TextMeshProUGUI stats, out Button menuButton)
        {
            RectTransform rt = Stretch("EndScreen", root);
            end = rt.gameObject;
            Fill(rt, Scrim);

            RectTransform card = Place("Card", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 320f));
            Fill(card, Card);

            var top = new Vector2(0.5f, 1f);
            title = Label("EndTitle", Place("EndTitleRect", card, top, new Vector2(0f, -56f), new Vector2(840f, 70f)),
                "", 44f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            stats = Label("EndStats", Place("EndStatsRect", card, top, new Vector2(0f, -144f), new Vector2(840f, 32f)),
                "", 22f, TextAlignmentOptions.Center, Muted);

            menuButton = MakeButton("EndMenuButton", card, "Zurück zum Menü", top, new Vector2(0f, -204f), new Vector2(320f, 50f));

            end.SetActive(false);
        }
    }
}
