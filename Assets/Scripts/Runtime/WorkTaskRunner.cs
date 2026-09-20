using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeImposter
{
    public enum WorkTaskKind
    {
        Typing = 0,
        Numbers = 1,
        HoldRelease = 2,
    }

    // The "pretending to work" minigames. Owner-side: they drive the prompts and report
    // results to the server, which is what actually pays out suspicion.
    [RequireComponent(typeof(PlayerStatus))]
    public class WorkTaskRunner : NetworkBehaviour
    {
        static readonly Key[] Letters = { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L };
        static readonly Key[] Digits =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        [SerializeField] int baseSequenceLength = 5;
        [SerializeField] float mistakePenalty = 4f;
        [SerializeField] float completionReward = 14f;
        [SerializeField] float holdFillSeconds = 1.7f;

        PlayerStatus _status;
        readonly StringBuilder _display = new StringBuilder();
        Key[] _sequence;
        int _index;

        float _holdValue;
        float _holdMin;
        float _holdMax;
        bool _holdWasPressed;

        public bool IsActive { get; private set; }
        public WorkTaskKind Kind { get; private set; }
        public int Completed { get; private set; }
        public string Sequence { get; private set; } = string.Empty;
        public int Index => _index;
        public string TaskName { get; private set; } = string.Empty;

        public float HoldValue => _holdValue;
        public float HoldMin => _holdMin;
        public float HoldMax => _holdMax;

        static readonly string[] TypingNames = { "Quartalsbericht tippen", "Protokoll schreiben", "Mails beantworten" };
        static readonly string[] NumberNames = { "Tabelle befüllen", "Zahlen abgleichen", "Rechnungen prüfen" };
        static readonly string[] HoldNames = { "Telefonat führen", "Datei hochladen", "Druckauftrag freigeben" };

        void Awake() => _status = GetComponent<PlayerStatus>();

        public void Begin()
        {
            if (!IsOwner || IsActive) return;

            IsActive = true;
            Kind = (WorkTaskKind)Random.Range(0, 3);
            NewRound();
            _status.SetWorkingServerRpc(true);
        }

        public void Cancel()
        {
            if (!IsActive) return;

            IsActive = false;
            Sequence = string.Empty;
            _index = 0;
            _holdValue = 0f;
            if (IsOwner) _status.SetWorkingServerRpc(false);
        }

        void NewRound()
        {
            switch (Kind)
            {
                case WorkTaskKind.Typing:
                    TaskName = TypingNames[Random.Range(0, TypingNames.Length)];
                    BuildSequence(Letters);
                    break;
                case WorkTaskKind.Numbers:
                    TaskName = NumberNames[Random.Range(0, NumberNames.Length)];
                    BuildSequence(Digits);
                    break;
                case WorkTaskKind.HoldRelease:
                    TaskName = HoldNames[Random.Range(0, HoldNames.Length)];
                    _holdValue = 0f;
                    _holdMin = Random.Range(0.55f, 0.72f);
                    _holdMax = _holdMin + (_status.IsExhausted ? 0.10f : 0.15f);
                    Sequence = string.Empty;
                    break;
            }
        }

        void BuildSequence(Key[] pool)
        {
            // Tired people fumble more: the sequence gets longer when energy is low.
            int length = baseSequenceLength + (_status.IsExhausted ? 2 : 0);

            _sequence = new Key[length];
            _display.Clear();

            for (int i = 0; i < length; i++)
            {
                _sequence[i] = pool[Random.Range(0, pool.Length)];
                if (i > 0) _display.Append(' ');
                _display.Append(Readable(_sequence[i]));
            }

            Sequence = _display.ToString();
            _index = 0;
        }

        static string Readable(Key key)
        {
            string name = key.ToString();
            return name.StartsWith("Digit") ? name.Substring(5) : name;
        }

        void Update()
        {
            if (!IsOwner || !IsActive) return;
            if (_status.IsCaught || _status.IsConfronted) { Cancel(); return; }

            var keyboard = Keyboard.current;
            if (keyboard == null || HudController.IsPaused) return;

            if (Kind == WorkTaskKind.HoldRelease) UpdateHold(keyboard);
            else UpdateSequence(keyboard, Kind == WorkTaskKind.Typing ? Letters : Digits);
        }

        void UpdateSequence(Keyboard keyboard, Key[] pool)
        {
            foreach (Key key in pool)
            {
                if (!keyboard[key].wasPressedThisFrame) continue;

                if (key == _sequence[_index])
                {
                    _index++;
                    if (_index >= _sequence.Length) FinishRound();
                    else AudioDirector.KeyClack();
                }
                else
                {
                    _index = 0;
                    Fail();
                }

                return;
            }
        }

        void UpdateHold(Keyboard keyboard)
        {
            bool pressed = keyboard.spaceKey.isPressed;

            if (pressed)
            {
                _holdValue = Mathf.Min(1f, _holdValue + Time.deltaTime / holdFillSeconds);
                _holdWasPressed = true;

                // Overshooting the window counts as botching it.
                if (_holdValue >= 1f)
                {
                    _holdWasPressed = false;
                    _holdValue = 0f;
                    Fail();
                }
                return;
            }

            if (!_holdWasPressed) return;
            _holdWasPressed = false;

            bool inWindow = _holdValue >= _holdMin && _holdValue <= _holdMax;
            _holdValue = 0f;

            if (inWindow) FinishRound();
            else Fail();
        }

        void FinishRound()
        {
            Completed++;
            AudioDirector.Success();
            CompleteServerRpc();
            NewRound();
        }

        void Fail()
        {
            AudioDirector.Mistake();
            PenaliseServerRpc();
        }

        [ServerRpc]
        void CompleteServerRpc()
        {
            _status.AddSuspicion(-completionReward);
            _status.CountTask();
        }

        [ServerRpc]
        void PenaliseServerRpc()
        {
            _status.AddSuspicion(mistakePenalty, SuspicionReason.Typo);
        }
    }
}
