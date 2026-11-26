namespace WordleResearch.Core
{
    public static class GameConfig
    {
        public const int WordLength = 5;
        public static System.Collections.Generic.IReadOnlyList<int> BoardCounts { get; } =
            System.Array.AsReadOnly(new[] { 1, 4, 8, 16 });

        private static int _boardCount = 1;

        public static int BoardCount
        {
            get => _boardCount;
            set => _boardCount = value is 1 or 4 or 8 or 16
                ? value
                : throw new System.ArgumentOutOfRangeException(nameof(value));
        }

        // 是否開啟 AI 決策輔助系統
        public static bool EnableAiAssist { get; set; } = true;

        // N + 5 步數規則
        public static int MaxGuesses => BoardCount + 5;
    }
}
