using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeImposter
{
    // The "pretending to work" minigame. Owner-side: it drives the key sequence and
    // reports completions to the server, which is what actually pays out suspicion.
    [RequireComponent(typeof(PlayerStatus))]
    public class WorkTaskRunner : NetworkBehaviour
    {
        static readonly Key[] Candidates =
        {
            Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L
        };

        [SerializeField] int sequenceLength = 5;
        [SerializeField] float mistakePenalty = 4f;

        PlayerStatus _status;
        readonly StringBuilder _display = new StringBuilder();
        Key[] _sequence;
        int _index;

        public bool IsActive { get; private set; }
        public int Completed { get; private set; }
        public string Sequence { get; private set; } = string.Empty;
        public int Index => _index;
        public string TaskName { get; private set; } = string.Empty;

        static readonly string[] TaskNames =
        {
            "Quartalsbericht tippen", "Tabelle befüllen", "Mails beantworten",
            "Tickets abarbeiten", "Protokoll schreiben",
        };

        void Awake() => _status = GetComponent<PlayerStatus>();

        public void Begin()
        {
            if (!IsOwner || IsActive) return;

            IsActive = true;
            TaskName = TaskNames[Random.Range(0, TaskNames.Length)];
            NewSequence();
            _status.SetWorkingServerRpc(true);
        }

        public void Cancel()
        {
            if (!IsActive) return;

            IsActive = false;
            Sequence = string.Empty;
            _index = 0;
            if (IsOwner) _status.SetWorkingServerRpc(false);
        }

        void NewSequence()
        {
            _sequence = new Key[sequenceLength];
            _display.Clear();

            for (int i = 0; i < sequenceLength; i++)
            {
                _sequence[i] = Candidates[Random.Range(0, Candidates.Length)];
                if (i > 0) _display.Append(' ');
                _display.Append(_sequence[i].ToString());
            }

            Sequence = _display.ToString();
            _index = 0;
        }

        void Update()
        {
            if (!IsOwner || !IsActive) return;
            if (_status.IsCaught) { Cancel(); return; }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            foreach (Key key in Candidates)
            {
                if (!keyboard[key].wasPressedThisFrame) continue;

                if (key == _sequence[_index])
                {
                    _index++;
                    if (_index >= _sequence.Length)
                    {
                        Completed++;
                        AudioDirector.Success();
                        CompleteServerRpc();
                        NewSequence();
                    }
                    else
                    {
                        AudioDirector.KeyClack();
                    }
                }
                else
                {
                    // Mashing is worse than typing nothing: the screen still looks wrong.
                    _index = 0;
                    AudioDirector.Mistake();
                    PenaliseServerRpc();
                }

                break;
            }
        }

        [ServerRpc]
        void CompleteServerRpc()
        {
            _status.AddSuspicion(-14f);
        }

        [ServerRpc]
        void PenaliseServerRpc()
        {
            _status.AddSuspicion(mistakePenalty);
        }
    }
}
