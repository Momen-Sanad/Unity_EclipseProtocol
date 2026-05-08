namespace EclipseProtocol.Core
{
    public static class RunSeedData
    {
        public static bool HasSeed { get; private set; }
        public static bool HasUserSeed { get; private set; }
        public static string SeedText { get; private set; } = string.Empty;
        public static int Seed { get; private set; }

        public static void SetSeed(string seedText)
        {
            SeedText = seedText != null ? seedText.Trim() : string.Empty;
            HasUserSeed = !string.IsNullOrEmpty(SeedText);
            if (!HasUserSeed)
            {
                UseRandomSeed();
                return;
            }

            HasSeed = true;
            Seed = BuildStableSeed(SeedText);
        }

        public static void UseRandomSeed()
        {
            SeedText = string.Empty;
            HasSeed = true;
            HasUserSeed = false;
            Seed = BuildRandomSeed();
        }

        public static int GetOrCreateSeed()
        {
            if (!HasSeed)
            {
                UseRandomSeed();
            }

            return Seed;
        }

        private static int BuildRandomSeed()
        {
            unchecked
            {
                return System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode();
            }
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
