using System;

/// <summary>Bounded configuration for a tile world. Surface heights include the top solid tile.</summary>
[Serializable]
public sealed class WorldGenerationSettings
{
    public int Width = 60;
    public int Height = 34;
    public int SurfaceHeight = 23;
    public int SurfaceVariation = 5;
    public double SurfaceScale = 0.06;
    public double CaveFill = 0.45;
    public int SmoothPasses = 3;
}

/// <summary>Air is 0, solid is 1; tile coordinates use an upward-positive Y axis.</summary>
public sealed class WorldData
{
    public double CellSize = 1;
    public TraversalPlan Route;
    public TileRegion[] ClimbRegions, VoidRegions;
    public int RequiredFragments;
    public int ExplorationNode = -1;
    public int[] FragmentNodes, CollectionLinks;
    public int Seed;
    public int[,] Tiles;
    public int[] Surface;
    public int SpawnX;
    public int SpawnY;
    public int GoalX;
    public int GoalY;
}

/// <summary>
/// Pure, repeatable terrain generation. All air is topologically connected, while only the
/// three-cell-high main gallery has a guaranteed continuous walking floor.
/// </summary>
public static class ProceduralWorld
{
    public static WorldData GenerateTraversal(TraversalGenerationSettings settings, TraversalCapabilities capabilities, StaminaSettings stamina, int? seed = null)
    {
        return HardcoreWorldGenerator.Generate(settings, capabilities, stamina, seed);
    }

    public static double SampleSurface(double position, int seed) { return Perlin(position, seed); }

    private const int GalleryFloor = 4;
    private const int GalleryBottom = 5;
    private const int GalleryTop = 7;

    public static WorldData Generate(WorldGenerationSettings settings, int? seed = null)
    {
        Validate(settings);
        int actualSeed = seed ?? BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
        int width = settings.Width;
        int height = settings.Height;
        int[,] tiles = new int[width, height];
        int[] surface = new int[width];
        LocalRandom random = new LocalRandom(actualSeed);
        double offset = random.NextUnit() * 256.0;

        for (int x = 0; x < width; x++)
        {
            double noise = Perlin(x * settings.SurfaceScale + offset, actualSeed);
            surface[x] = settings.SurfaceHeight + (int)Math.Round(noise * settings.SurfaceVariation, MidpointRounding.AwayFromZero);
            for (int y = 0; y < height; y++)
            {
                bool shell = x == 0 || x == width - 1 || y <= GalleryFloor;
                tiles[x, y] = shell || y <= surface[x] ? 1 : 0;
                if (!shell && y >= GalleryBottom && y < surface[x] - 1)
                    tiles[x, y] = random.NextUnit() < settings.CaveFill ? 1 : 0;
            }
        }

        tiles = SmoothCaves(tiles, surface, settings.SmoothPasses);
        for (int x = 1; x < width - 1; x++)
            for (int y = GalleryBottom; y <= GalleryTop; y++)
                tiles[x, y] = 0;

        ConnectAirToGallery(tiles);
        return new WorldData
        {
            Seed = actualSeed,
            Tiles = tiles,
            Surface = surface,
            SpawnX = 3,
            SpawnY = GalleryBottom,
            GoalX = width - 4,
            GoalY = GalleryBottom
        };
    }

    private static void Validate(WorldGenerationSettings settings)
    {
        if (settings == null) throw new ArgumentNullException("settings");
        if (settings.Width < 24 || settings.Width > 256)
            throw new ArgumentException("Width must be between 24 and 256 tiles.", "settings");
        if (settings.Height < 20 || settings.Height > 128)
            throw new ArgumentException("Height must be between 20 and 128 tiles.", "settings");
        if (settings.SurfaceVariation < 0 || settings.SurfaceVariation > 32)
            throw new ArgumentException("SurfaceVariation must be between 0 and 32 tiles.", "settings");
        // Compare the height directly to avoid overflow from an invalid extreme integer.
        if (settings.SurfaceHeight < 10 + settings.SurfaceVariation ||
            settings.SurfaceHeight > settings.Height - 3 - settings.SurfaceVariation)
            throw new ArgumentException("The complete surface envelope must remain between Y=10 and Height-3.", "settings");
        if (double.IsNaN(settings.SurfaceScale) || settings.SurfaceScale < 0.005 || settings.SurfaceScale > 1.0)
            throw new ArgumentException("SurfaceScale must be finite and between 0.005 and 1.", "settings");
        if (double.IsNaN(settings.CaveFill) || settings.CaveFill < 0.0 || settings.CaveFill > 1.0)
            throw new ArgumentException("CaveFill must be finite and between 0 and 1.", "settings");
        if (settings.SmoothPasses < 0 || settings.SmoothPasses > 8)
            throw new ArgumentException("SmoothPasses must be between 0 and 8.", "settings");
    }

    private static int[,] SmoothCaves(int[,] tiles, int[] surface, int passes)
    {
        // Both buffers start with the same shell and sky; only underground cells change.
        int[,] next = (int[,])tiles.Clone();
        int width = tiles.GetLength(0);
        for (int pass = 0; pass < passes; pass++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = GalleryBottom; y < surface[x] - 1; y++)
                {
                    int neighbours = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (dx != 0 || dy != 0) neighbours += tiles[x + dx, y + dy];
                    next[x, y] = neighbours >= 5 ? 1 : 0;
                }
            }
            int[,] swap = tiles;
            tiles = next;
            next = swap;
        }
        return tiles;
    }

    private static void ConnectAirToGallery(int[,] tiles)
    {
        int width = tiles.GetLength(0);
        int height = tiles.GetLength(1);
        bool[,] reached = new bool[width, height];
        int[] queue = new int[width * height];
        int head = 0;
        int tail = 0;
        Enqueue(tiles, reached, queue, 3, GalleryBottom, width, ref tail);
        Flood(tiles, reached, queue, width, height, ref head, ref tail);

        for (int y = GalleryBottom; y < height; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (tiles[x, y] != 0 || reached[x, y]) continue;
                // Join an unvisited component straight down to the reserved gallery.
                // Every connector is two cells wide and remains inside the side shell.
                int secondX = x < width - 2 ? x + 1 : x - 1;
                for (int shaftY = GalleryBottom; shaftY <= y; shaftY++)
                {
                    tiles[x, shaftY] = 0;
                    tiles[secondX, shaftY] = 0;
                    Enqueue(tiles, reached, queue, x, shaftY, width, ref tail);
                    Enqueue(tiles, reached, queue, secondX, shaftY, width, ref tail);
                }
                Flood(tiles, reached, queue, width, height, ref head, ref tail);
            }
        }
    }

    private static void Flood(int[,] tiles, bool[,] reached, int[] queue, int width, int height, ref int head, ref int tail)
    {
        while (head < tail)
        {
            int position = queue[head++];
            int x = position % width;
            int y = position / width;
            if (x > 1) Enqueue(tiles, reached, queue, x - 1, y, width, ref tail);
            if (x < width - 2) Enqueue(tiles, reached, queue, x + 1, y, width, ref tail);
            if (y > GalleryBottom) Enqueue(tiles, reached, queue, x, y - 1, width, ref tail);
            if (y < height - 1) Enqueue(tiles, reached, queue, x, y + 1, width, ref tail);
        }
    }

    private static void Enqueue(int[,] tiles, bool[,] reached, int[] queue, int x, int y, int width, ref int tail)
    {
        if (reached[x, y] || tiles[x, y] != 0) return;
        reached[x, y] = true;
        queue[tail++] = y * width + x;
    }

    private static double Perlin(double position, int seed)
    {
        int cell = (int)Math.Floor(position);
        double t = position - cell;
        double fade = t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
        double left = (Hash(unchecked((uint)cell), seed) & 1) == 0 ? t : -t;
        double right = (Hash(unchecked((uint)(cell + 1)), seed) & 1) == 0 ? t - 1.0 : 1.0 - t;
        return (left + fade * (right - left)) * 2.0;
    }

    private static uint Hash(uint value, int seed)
    {
        unchecked
        {
            value ^= (uint)seed;
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            return value ^ (value >> 16);
        }
    }

    // Fixed SplitMix32 transition; reproducibility does not depend on System.Random or Unity.Random.
    private struct LocalRandom
    {
        private uint state;

        public LocalRandom(int seed) { state = unchecked((uint)seed); }

        public double NextUnit()
        {
            unchecked
            {
                state += 0x9e3779b9u;
                uint value = state;
                value = (value ^ (value >> 16)) * 0x21f0aaadu;
                value = (value ^ (value >> 15)) * 0x735a2d97u;
                value ^= value >> 15;
                return value / 4294967296.0;
            }
        }
    }
}
