namespace EclipseProtocol.Core
{
    public static class RunDifficultyData
    {
        public static DifficultyMode CurrentDifficulty { get; private set; } = DifficultyMode.Medium;

        public static void SetDifficulty(DifficultyMode difficulty)
        {
            CurrentDifficulty = difficulty;
        }
    }
}
