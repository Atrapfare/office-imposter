namespace OfficeImposter
{
    // The boss's spot-checks. Every wrong answer is something a real employee would
    // never say, which is the joke: you have to guess what your job sounds like.
    public static class ConfrontationBank
    {
        public struct Question
        {
            public string Text;
            public string[] Answers;
            public int Correct;
        }

        static readonly Question[] Questions =
        {
            new Question
            {
                Text = "Wie weit sind Sie mit dem Quartalsbericht?",
                Answers = new[] { "Zahlen stehen, ich prüfe noch die Quellen.", "Welcher Bericht?", "Ich habe ihn gelöscht." },
                Correct = 1,
            },
            new Question
            {
                Text = "Haben Sie das Ticket von gestern abgeschlossen?",
                Answers = new[] { "Ich warte noch auf Rückmeldung vom Kunden.", "Ich mache keine Tickets.", "Tickets sind überbewertet." },
                Correct = 1,
            },
            new Question
            {
                Text = "Was sagen die aktuellen Zahlen?",
                Answers = new[] { "Sehr gut, sehr gut.", "Leicht über Plan, Details im Meeting.", "Zahlen lügen sowieso." },
                Correct = 2,
            },
            new Question
            {
                Text = "Warum stehen Sie hier herum?",
                Answers = new[] { "Ich hatte eine Frage an einen Kollegen.", "Ich mache nichts.", "Das geht Sie nichts an." },
                Correct = 1,
            },
            new Question
            {
                Text = "Brauchen Sie für irgendetwas Unterstützung?",
                Answers = new[] { "Nein, alles unter Kontrolle.", "Ja, was ist eigentlich mein Job?", "Ich brauche einen neuen Stuhl." },
                Correct = 1,
            },
            new Question
            {
                Text = "Haben Sie meine Mail von heute Morgen gesehen?",
                Answers = new[] { "Welche Mail?", "Ja, ich antworte nach dem Meeting.", "Ich lese keine Mails." },
                Correct = 2,
            },
        };

        public static int Count => Questions.Length;

        public static Question Get(int index)
        {
            if (index < 0 || index >= Questions.Length) return Questions[0];
            return Questions[index];
        }
    }
}
