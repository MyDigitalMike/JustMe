param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\..\Assets\Script\GeneracionMundo\ProceduralWorld.cs'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\Backups\Phase2\world-checks.json')
)

$ErrorActionPreference = 'Stop'
$priorFailure = $null
if (Test-Path -LiteralPath $OutputPath) {
    $previous = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
    if ($previous.testFirstEvidence) { $priorFailure = $previous.testFirstEvidence }
    elseif ($previous.passed -eq $false) { $priorFailure = $previous.failure }
}
$report = [ordered]@{
    passed = $false
    verification = 'PowerShell Add-Type: pure C# contract checks; not a Unity compilation or playtest'
    testFirstEvidence = $priorFailure
}

try {
    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw 'Missing generator API: ProceduralWorld.cs does not exist.'
    }
    $checks = @'
public static class WorldGenerationContractChecks
{
    public static int Assertions;
    public static int MapsChecked;
    public static readonly int[] Seeds = { 0, 1, -1, 42, -90210, 8675309, int.MinValue, int.MaxValue };
    public static int RandomSeed;

    private static void Check(bool condition, string description)
    {
        Assertions++;
        if (!condition) throw new Exception(description);
    }

    private static bool Same(WorldData a, WorldData b)
    {
        if (a.Seed != b.Seed || a.SpawnX != b.SpawnX || a.SpawnY != b.SpawnY ||
            a.GoalX != b.GoalX || a.GoalY != b.GoalY ||
            a.Tiles.GetLength(0) != b.Tiles.GetLength(0) || a.Tiles.GetLength(1) != b.Tiles.GetLength(1)) return false;
        for (int x = 0; x < a.Surface.Length; x++)
        {
            if (a.Surface[x] != b.Surface[x]) return false;
            for (int y = 0; y < a.Tiles.GetLength(1); y++)
                if (a.Tiles[x, y] != b.Tiles[x, y]) return false;
        }
        return true;
    }

    private static void CheckWorld(WorldGenerationSettings settings, WorldData world, bool variedSurface)
    {
        MapsChecked++;
        int w = settings.Width, h = settings.Height;
        Check(world.Tiles.GetLength(0) == w && world.Tiles.GetLength(1) == h, "Map dimensions");
        Check(world.Surface.Length == w, "Surface dimensions");
        Check(world.SpawnX == 3 && world.SpawnY == 5 && world.GoalX == w - 4 && world.GoalY == 5, "Stable spawn/goal contract");
        int air = 0, minimum = h, maximum = 0, visibleSurface = 0;
        for (int x = 0; x < w; x++)
        {
            Check(world.Surface[x] >= settings.SurfaceHeight - settings.SurfaceVariation &&
                  world.Surface[x] <= settings.SurfaceHeight + settings.SurfaceVariation, "Surface stays within configured envelope");
            minimum = Math.Min(minimum, world.Surface[x]);
            maximum = Math.Max(maximum, world.Surface[x]);
            if (x > 0 && x < w - 1 && world.Tiles[x, world.Surface[x]] == 1) visibleSurface++;
            for (int y = 0; y < h; y++)
            {
                int tile = world.Tiles[x, y];
                Check(tile == 0 || tile == 1, "Only air/solid tile values");
                if (tile == 0) air++;
                if (x == 0 || x == w - 1 || y <= 4) Check(tile == 1, "Solid side/bottom shell and continuous gallery floor");
                if (x > 0 && x < w - 1 && y >= 5 && y <= 7) Check(tile == 0, "Three-high continuous main gallery");
                if (x > 0 && x < w - 1 && y > world.Surface[x]) Check(tile == 0, "Open air above surface");
            }
        }
        Check(!variedSurface || maximum > minimum, "Default surface has observable height variation");
        Check(visibleSurface >= (w - 2) / 2, "Most of the nominal surface remains visible after carving connectors");
        for (int x = world.SpawnX; x < world.SpawnX + 2; x++)
        {
            Check(world.Tiles[x, world.SpawnY - 1] == 1, "Two-cell spawn floor");
            for (int y = world.SpawnY; y < world.SpawnY + 3; y++)
                Check(world.Tiles[x, y] == 0, "Two-by-three spawn clearance");
        }
        // Independent four-neighbour flood checks every cave and the air above the surface.
        bool[,] seen = new bool[w, h];
        int[] queue = new int[w * h];
        int head = 0, tail = 0;
        queue[tail++] = world.SpawnY * w + world.SpawnX;
        seen[world.SpawnX, world.SpawnY] = true;
        int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
        while (head < tail)
        {
            int p = queue[head++], x = p % w, y = p / w;
            for (int n = 0; n < 4; n++)
            {
                int nx = x + dx[n], ny = y + dy[n];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h || seen[nx, ny] || world.Tiles[nx, ny] != 0) continue;
                seen[nx, ny] = true;
                queue[tail++] = ny * w + nx;
            }
        }
        Check(tail == air, "Every air component connects to the gallery, including exterior air");
        Check(seen[world.GoalX, world.GoalY], "Goal connects to spawn");
        // A solid walk floor and three-cell headroom guarantee this horizontal route independently of jump tuning.
        for (int x = world.SpawnX; x <= world.GoalX; x++)
            Check(world.Tiles[x, 4] == 1 && world.Tiles[x, 5] == 0 && world.Tiles[x, 6] == 0 && world.Tiles[x, 7] == 0,
                "Spawn-to-goal walking route");
    }

    private static void Invalid(Action<WorldGenerationSettings> edit, string description)
    {
        WorldGenerationSettings settings = new WorldGenerationSettings();
        edit(settings);
        bool rejected = false;
        try { ProceduralWorld.Generate(settings, 42); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Reject invalid setting: " + description);
    }

    public static void Run()
    {
        Check(Attribute.IsDefined(typeof(WorldGenerationSettings), typeof(SerializableAttribute)), "Settings are serializable");
        WorldGenerationSettings defaults = new WorldGenerationSettings();
        Check(defaults.Width == 60 && defaults.Height == 34 && defaults.SurfaceHeight == 23 && defaults.SurfaceVariation == 5 &&
              defaults.SurfaceScale == 0.06 && defaults.CaveFill == 0.45 && defaults.SmoothPasses == 3, "Documented default configuration");
        WorldData first = null;
        for (int i = 0; i < Seeds.Length; i++)
        {
            WorldData world = ProceduralWorld.Generate(defaults, Seeds[i]);
            Check(world.Seed == Seeds[i], "Preserve explicit signed seed");
            CheckWorld(defaults, world, true);
            Check(Same(world, ProceduralWorld.Generate(defaults, Seeds[i])), "Same seed repeats complete map");
            if (i == 0) first = world;
            else Check(!Same(first, world), "Different seeds change generated tiles/surface");
        }
        // Compare the terrain, ignoring the seed field itself: relabelling a constant map must fail.
        WorldData other = ProceduralWorld.Generate(defaults, 1);
        other.Seed = first.Seed;
        Check(!Same(first, other), "Different seeds produce different terrain, not just different metadata");
        WorldData random = ProceduralWorld.Generate(defaults);
        RandomSeed = random.Seed;
        CheckWorld(defaults, random, true);
        Check(Same(random, ProceduralWorld.Generate(defaults, RandomSeed)), "Automatically chosen seed is replayable");

        WorldGenerationSettings small = new WorldGenerationSettings { Width = 24, Height = 20, SurfaceHeight = 13, SurfaceVariation = 3 };
        CheckWorld(small, ProceduralWorld.Generate(small, int.MinValue), false);
        WorldGenerationSettings large = new WorldGenerationSettings { Width = 256, Height = 128, SurfaceHeight = 80, SurfaceVariation = 32 };
        CheckWorld(large, ProceduralWorld.Generate(large, int.MaxValue), true);
        WorldGenerationSettings emptyCaves = new WorldGenerationSettings { CaveFill = 0, SmoothPasses = 0, SurfaceVariation = 0, SurfaceScale = 0.005 };
        WorldGenerationSettings solidCaves = new WorldGenerationSettings { CaveFill = 1, SmoothPasses = 8, SurfaceScale = 1 };
        CheckWorld(emptyCaves, ProceduralWorld.Generate(emptyCaves, 0), false);
        CheckWorld(solidCaves, ProceduralWorld.Generate(solidCaves, 0), false);
        WorldGenerationSettings fillA = new WorldGenerationSettings { CaveFill = 0, SmoothPasses = 0 };
        WorldGenerationSettings fillB = new WorldGenerationSettings { CaveFill = 1, SmoothPasses = 0 };
        Check(!Same(ProceduralWorld.Generate(fillA, 42), ProceduralWorld.Generate(fillB, 42)), "Cave fill changes underground terrain");
        WorldGenerationSettings unsmoothed = new WorldGenerationSettings { SmoothPasses = 0 };
        Check(!Same(ProceduralWorld.Generate(unsmoothed, 42), ProceduralWorld.Generate(defaults, 42)), "Smoothing changes the cave distribution");
        WorldData a = ProceduralWorld.Generate(defaults, 123);
        a.Tiles[3, 5] = 1;
        Check(ProceduralWorld.Generate(defaults, 123).Tiles[3, 5] == 0, "Returned maps own independent arrays");
        Check(defaults.Width == 60 && defaults.Height == 34 && defaults.SurfaceHeight == 23 && defaults.CaveFill == .45,
            "Generation does not mutate caller configuration");

        bool nullRejected = false;
        try { ProceduralWorld.Generate(null); }
        catch (ArgumentException) { nullRejected = true; }
        Check(nullRejected, "Null settings are rejected");
        Invalid(s => s.Width = 23, "width below minimum");
        Invalid(s => s.Width = 257, "width above maximum");
        Invalid(s => s.Height = 19, "height below minimum");
        Invalid(s => s.Height = 129, "height above maximum");
        Invalid(s => s.Width = int.MaxValue, "dimension overflow");
        Invalid(s => s.SurfaceVariation = -1, "negative variation");
        Invalid(s => s.SurfaceVariation = 33, "unbounded variation");
        Invalid(s => s.SurfaceHeight = 14, "surface reaches gallery");
        Invalid(s => s.SurfaceHeight = 27, "surface leaves no sky");
        Invalid(s => s.SurfaceScale = 0, "zero scale");
        Invalid(s => s.SurfaceScale = 1.1, "excessive scale");
        Invalid(s => s.SurfaceScale = double.NaN, "NaN scale");
        Invalid(s => s.SurfaceScale = double.PositiveInfinity, "infinite scale");
        Invalid(s => s.CaveFill = -0.1, "negative fill");
        Invalid(s => s.CaveFill = 1.1, "fill above one");
        Invalid(s => s.CaveFill = double.NaN, "NaN fill");
        Invalid(s => s.CaveFill = double.NegativeInfinity, "infinite fill");
        Invalid(s => s.SmoothPasses = -1, "negative smoothing");
        Invalid(s => s.SmoothPasses = 9, "unbounded smoothing");
    }
}
'@
    $projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $units=@($SourcePath,(Join-Path $projectRoot 'Assets/Script/Game/TraversalStamina.cs'),(Join-Path $projectRoot 'Assets/Script/GeneracionMundo/TraversalWorld.cs'),(Join-Path $projectRoot 'Assets/Script/GeneracionMundo/TraversalValidator.cs'))
    $definitions=foreach($unit in $units){[regex]::Replace((Get-Content -LiteralPath $unit -Raw),'(?m)^using [^;]+;\s*','')}
    Add-Type -TypeDefinition ('using System; using System.Collections.Generic;' + [Environment]::NewLine + ($definitions -join [Environment]::NewLine) + [Environment]::NewLine + $checks) -Language CSharp
    [WorldGenerationContractChecks]::Run()
    $report.passed = $true
    $report.assertions = [WorldGenerationContractChecks]::Assertions
    $report.mapsChecked = [WorldGenerationContractChecks]::MapsChecked
    $report.seeds = [WorldGenerationContractChecks]::Seeds
    $report.automaticSeedReplayed = [WorldGenerationContractChecks]::RandomSeed
    $report.checks = @('determinism', 'seed variation', 'replayable automatic seed', 'air connectivity', 'gallery/floor', 'spawn clearance', 'surface bounds/variation/visibility', 'cave fill/smoothing', 'dimension limits', 'invalid settings', 'independent maps')
    $report.limitation = 'Connectivity is topological. Only the main horizontal gallery is guaranteed walkable; cave ledges are not claimed jump-reachable.'
}
catch {
    $report.failure = $_.Exception.Message
    if ($report.failure -like 'Missing generator API:*') { $report.testFirstEvidence = $report.failure }
    throw
}
finally {
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $outputDirectory)) { New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null }
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    $report | ConvertTo-Json -Depth 4 | Write-Output
}
