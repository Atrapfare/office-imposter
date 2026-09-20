using UnityEngine;

namespace OfficeImposter
{
    // Printer, coffee machine, filing cabinet, the boss's in-tray. One component,
    // behaviour switched by kind, so the scene builder can place them freely.
    public class JobStation : Interactable
    {
        [SerializeField] StationKind kind = StationKind.Printer;
        [SerializeField] string label = "Station";
        [SerializeField] Transform focus;

        public StationKind Kind => kind;
        public override Vector3 FocusPoint => focus != null ? focus.position : transform.position;

        public void Configure(StationKind stationKind, string stationLabel, Transform focusPoint)
        {
            kind = stationKind;
            label = stationLabel;
            focus = focusPoint;
        }

        public override string Prompt
        {
            get
            {
                var status = PlayerStatus.Local;
                if (status != null && status.HasJob)
                {
                    var job = (JobKind)status.JobKind.Value;
                    int stage = status.JobStage.Value;

                    bool relevant =
                        (kind == StationKind.Printer && job == JobKind.PrintReport && stage == 1) ||
                        (kind == StationKind.Coffee && job == JobKind.FetchCoffee && stage == 1) ||
                        (kind == StationKind.Cabinet && job == JobKind.FileDocuments) ||
                        (kind == StationKind.Delivery && stage == 2);

                    if (relevant) return $"[E]   {label}  ·  Auftrag erledigen";
                }

                if (kind == StationKind.Coffee) return $"[E]   {label}  ·  Kaffee trinken";
                if (kind == StationKind.WaterCooler) return $"[E]   {label}  ·  kurz durchatmen";
                return $"[E]   {label}";
            }
        }

        public override void Interact(PlayerController player)
        {
            // Interactables are plain MonoBehaviours, so the request travels through the
            // player's own NetworkBehaviour; the server decides what the station gives.
            if (player.Status == null) return;

            player.Status.UseStationServerRpc((int)kind);
            AudioDirector.Success();
        }
    }
}
