using System;

// Run-local seed policy; callers supply the existing validated world factory.
public sealed class WorldRunSession
{
    readonly Func<int> seedFactory;
    readonly int maximumAttempts;
    public int? CurrentSeed { get; private set; }
    public int CurrentLevel { get; private set; }
    public string CurrentSignature { get; private set; }

    public WorldRunSession(Func<int> seedFactory = null, int maximumAttempts = 8)
    {
        if (maximumAttempts < 1 || maximumAttempts > 32) throw new ArgumentOutOfRangeException("maximumAttempts");
        this.seedFactory = seedFactory ?? (() => BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0));
        this.maximumAttempts = maximumAttempts;
    }

    public WorldData GenerateNext(int level, bool fixedSeed, int debugSeed, Func<int, WorldData> generateValidatedCandidate)
    {
        if (level < 1) throw new ArgumentOutOfRangeException("level");
        if (generateValidatedCandidate == null) throw new ArgumentNullException("generateValidatedCandidate");
        string lastFailure = "Consecutive seed or geometry repeated.";
        for (int attempt = 0; attempt < (fixedSeed ? 1 : maximumAttempts); attempt++)
        {
            int seed = fixedSeed ? unchecked(debugSeed + level - 1) : seedFactory();
            if (!fixedSeed && CurrentSeed == seed) continue;
            WorldData candidate;
            try { candidate = generateValidatedCandidate(seed); }
            catch (TraversalGenerationException e) { lastFailure = e.Message; if (fixedSeed) throw; else continue; }
            if (candidate == null || candidate.Seed != seed) throw new InvalidOperationException("Validated world factory returned no matching seed.");
            string signature = GeometrySignature(candidate);
            if (!fixedSeed && signature == CurrentSignature) continue;
            CurrentSeed = seed; CurrentSignature = signature; CurrentLevel = level;
            return candidate;
        }
        throw new InvalidOperationException("No distinct validated world accepted after " + maximumAttempts + " attempts. " + lastFailure);
    }

    public static string GeometrySignature(WorldData world)
    {
        if (world == null || world.Tiles == null) throw new ArgumentException("World geometry is required.");
        // FNV-1a is a bounded local geometry comparison, not a security digest. Exclude seed/pages.
        ulong hash = 14695981039346656037UL;
        unchecked
        {
            hash = (hash ^ (uint)world.Tiles.GetLength(0)) * 1099511628211UL;
            hash = (hash ^ (uint)world.Tiles.GetLength(1)) * 1099511628211UL;
            hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(world.CellSize)) * 1099511628211UL;
            foreach (int tile in world.Tiles) hash = (hash ^ (uint)tile) * 1099511628211UL;
        }
        return hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
