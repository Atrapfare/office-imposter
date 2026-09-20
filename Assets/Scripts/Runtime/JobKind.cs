namespace OfficeImposter
{
    public enum JobKind
    {
        None = 0,
        PrintReport = 1,
        FetchCoffee = 2,
        FileDocuments = 3,
    }

    public enum StationKind
    {
        Printer = 0,
        Coffee = 1,
        Delivery = 2,
        Cabinet = 3,
        WaterCooler = 4,
    }

    public static class JobText
    {
        public static string Describe(JobKind kind, int stage)
        {
            switch (kind)
            {
                case JobKind.PrintReport:
                    return stage == 1 ? "Bericht am Drucker ausdrucken" : "Bericht zur Ablage des Chefs bringen";
                case JobKind.FetchCoffee:
                    return stage == 1 ? "Kaffee an der Maschine holen" : "Kaffee zum Chef bringen";
                case JobKind.FileDocuments:
                    return "Unterlagen im Aktenschrank ablegen";
                default:
                    return string.Empty;
            }
        }
    }
}
