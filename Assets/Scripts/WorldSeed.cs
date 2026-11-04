public static class WorldSeed
{
    public const int AsteroidPositions = 1;
    public const int AsteroidDensity = 2;
    public const int Dust = 3;
    public const int TinyDust = 4;
    public const int CloudDust = 5;

    public static int Derive(int worldSeed, int streamId)
    {
        unchecked
        {
            // A reversible mix within the nonnegative 31-bit range.
            // Distinct stream IDs produce distinct derived seed values
            // for the same world seed.
            uint value = ((uint)worldSeed ^ (uint)streamId)
                         & 0x7FFFFFFFu;

            value ^= value >> 16;
            value = (value * 0x045D9F3Bu) & 0x7FFFFFFFu;
            value ^= value >> 16;
            value = (value * 0x045D9F3Bu) & 0x7FFFFFFFu;
            value ^= value >> 16;

            return (int)value;
        }
    }
}