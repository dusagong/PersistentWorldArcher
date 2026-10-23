namespace PersistentWorld.World
{
    public static class DialogueState
    {
        public static string Speaker { get; private set; } = string.Empty;
        public static string Line { get; private set; } = string.Empty;

        public static void Show(string speaker, string line)
        {
            Speaker = speaker;
            Line = line;
        }

        public static void Clear()
        {
            Speaker = string.Empty;
            Line = string.Empty;
        }
    }
}

