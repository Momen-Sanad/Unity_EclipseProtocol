namespace EclipseProtocol.Core
{
    public static class RunSeedData
    {
        public static bool HasSeed { get; private set; }
        public static string SeedText { get; private set; } = string.Empty;
        public static int Seed { get; private set; }

        public static void SetSeed(string seedText)
        {
            SeedText = seedText != null ? seedText.Trim() : string.Empty;
            HasSeed = !string.IsNullOrEmpty(SeedText);
            Seed = HasSeed ? BuildStableSeed(SeedText) : System.Environment.TickCount;
        }

        public static int GetOrCreateSeed()
        {
            if (!HasSeed)
            {
                Seed = System.Environment.TickCount;
            }

            return Seed;
        }

        private static int BuildStableSeed(string text)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }

                return hash;
            }
        }
    }
}
