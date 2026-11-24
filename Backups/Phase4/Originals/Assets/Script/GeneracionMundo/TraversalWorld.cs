using System;
using System.Collections.Generic;

public enum WorldProfile { Horizontal, Ascent, Descent, Mixed }
public enum TraversalAction { Walk, Sprint, Jump, DoubleJump, Crawl, Climb, Rest, Drop }

[Serializable]
public sealed class TraversalGenerationSettings
{
    public WorldGenerationSettings Terrain = new WorldGenerationSettings();
    public WorldProfile Profile;
    public double CellSize = 0.5;
    public int MaximumAttempts = 4;
}

[Serializable]
public sealed class TrajectorySample
{
    public double Time, X, Y;
    public TrajectorySample() { }
    public TrajectorySample(double time, double x, double y) { Time = time; X = x; Y = y; }
}

[Serializable]
public sealed class TraversalCapabilities
{
    public double SourceWalkCommand, SourceMaxSpeed, SourceJumpImpulse, SourceGravityScale, SourceLinearDamping, SourceMass;
    public double SourceSprintMultiplier = 2.5, SourceCrawlMultiplier = .45, SourceClimbCommand = 1.5;
    public double SourceAcceleration, SourceDeceleration, SourceAirControl = 1, SourceCrouchRatio = .45;
    public double SourceJumpCooldown, SourceGravityX, SourceGravityY, SourceLedgeHeight = 1.05, SourceLedgeReach = .3, SourcePullUpSpeed = 1.5;
    public double StandingWidth, StandingHeight, CrouchedWidth, CrouchedHeight, SoleLocalY, CrouchedSoleLocalY;
    public double WalkSpeed, SprintSpeed, CrawlSpeed, ClimbSpeed;
    public double SingleJumpPeak, SingleJumpFlight, SingleJumpReach, DoubleJumpPeak, DoubleJumpFlight, DoubleJumpReach;
    public double DoubleJumpPressSeconds, JumpRunUpSeconds, PullUpSeconds, FixedStep;
    public TrajectorySample[] SingleJumpSamples, DoubleJumpSamples;
}

[Serializable]
public sealed class TraversalNode
{
    public int FloorX, FloorY;
    public bool SafeRest = true;
    public TraversalNode() { }
    public TraversalNode(int x, int y) { FloorX = x; FloorY = y; }
}

[Serializable]
public sealed class TraversalLink
{
    public int FromNode, ToNode;
    public TraversalAction Action;
    public double Duration;
    public bool Primary = true;
    public int BranchId;
    public double ClimbX;
    public TrajectorySample[] Path;
}

[Serializable]
public sealed class TraversalPlan
{
    public WorldProfile Profile;
    public TraversalNode[] Nodes;
    public TraversalLink[] Links;
    public int[] PrimaryLinks;
}

[Serializable]
public sealed class TileRegion
{
    public int MinX, MinY, MaxX, MaxY;
    public int CueX, CueY;
    public TileRegion() { }
    public TileRegion(int minX, int minY, int maxX, int maxY) { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
}

public sealed class TraversalGenerationException : Exception
{
    public readonly int Seed, Attempt;
    public readonly WorldProfile Profile;
    public TraversalGenerationException(int seed, WorldProfile profile, int attempt, string reason)
        : base("Traversal rejected: seed=" + seed + ", profile=" + profile + ", attempt=" + attempt + ": " + reason)
    { Seed = seed; Profile = profile; Attempt = attempt; }
}

/// <summary>Plans traversal first, seals its envelope, then reserves swept air and supporting tiles.</summary>
public static class HardcoreWorldGenerator
{
    public static WorldData Generate(TraversalGenerationSettings settings, TraversalCapabilities caps, StaminaSettings stamina, int? seed = null)
    {
        ValidateSettings(settings);
        TraversalValidator.ValidateCapabilities(caps);
        if (stamina == null) throw new ArgumentNullException("stamina");
        stamina.Validate();
        int actualSeed = seed ?? BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
        string failure = "No candidate attempted.";
        for (int attempt = 0; attempt < settings.MaximumAttempts; attempt++)
        {
            WorldData world = Build(settings, caps, actualSeed, attempt);
            TraversalValidation result = TraversalValidator.Validate(world, caps, stamina);
            if (result.Valid) return world;
            failure = "link=" + result.FailedLink + ": " + result.Reason;
        }
        throw new TraversalGenerationException(actualSeed, settings.Profile, settings.MaximumAttempts, failure);
    }

    private static void ValidateSettings(TraversalGenerationSettings s)
    {
        if (s == null || s.Terrain == null) throw new ArgumentException("Traversal settings and Terrain are required.");
        WorldGenerationSettings t = s.Terrain;
        if (t.Width < 56 || t.Width > 256 || t.Height < 30 || t.Height > 128)
            throw new ArgumentException("Traversal dimensions must be 56..256 by 30..128; smaller worlds cannot contain the hard route.");
        if (!Enum.IsDefined(typeof(WorldProfile), s.Profile) || s.CellSize != .5 || s.MaximumAttempts < 1 || s.MaximumAttempts > 12)
            throw new ArgumentException("Invalid profile, half-unit cell size or bounded attempt count.");
        if (t.SurfaceVariation < 0 || t.SurfaceVariation > 32 || t.SurfaceHeight < 10 + t.SurfaceVariation ||
            t.SurfaceHeight > t.Height - 3 - t.SurfaceVariation || !Finite(t.SurfaceScale) || t.SurfaceScale < .005 || t.SurfaceScale > 1 ||
            !Finite(t.CaveFill) || t.CaveFill < 0 || t.CaveFill > 1 || t.SmoothPasses < 0 || t.SmoothPasses > 8)
            throw new ArgumentException("Invalid terrain parameters.");
    }

    private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    private static uint Mix(uint value)
    {
        unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); }
    }

    private static WorldData Build(TraversalGenerationSettings s, TraversalCapabilities caps, int seed, int attempt)
    {
        int w = s.Terrain.Width, h = s.Terrain.Height;
        WorldData world = new WorldData { Seed = seed, CellSize = s.CellSize, Tiles = new int[w, h], Surface = new int[w] };
        uint random = Mix(unchecked((uint)seed) + (uint)attempt * 31u);
        double offset = (random & 65535u) / 256.0;
        for (int x = 0; x < w; x++)
        {
            world.Surface[x] = s.Terrain.SurfaceHeight + (int)Math.Round(ProceduralWorld.SampleSurface(x * s.Terrain.SurfaceScale + offset, seed) * s.Terrain.SurfaceVariation);
            for (int y = 0; y < h; y++)
            {
                int tile = x == 0 || x == w - 1 || y < 2 || y <= world.Surface[x] ? 1 : 0;
                if (x > 0 && x < w - 1 && y >= 2 && y < world.Surface[x] - 1)
                {
                    random = Mix(unchecked(random + 0x9e3779b9u));
                    tile = random / 4294967296.0 < s.Terrain.CaveFill ? 1 : 0;
                }
                world.Tiles[x, y] = tile;
            }
        }
        for (int pass = 0; pass < s.Terrain.SmoothPasses; pass++)
        {
            int[,] next = (int[,])world.Tiles.Clone();
            for (int x = 1; x < w - 1; x++) for (int y = 2; y < world.Surface[x] - 1; y++)
            {
                int count = 0;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) count += world.Tiles[x + dx, y + dy];
                next[x, y] = count >= 5 ? 1 : 0;
            }
            world.Tiles = next;
        }

        List<TraversalNode> nodes = new List<TraversalNode>();
        List<TraversalLink> links = new List<TraversalLink>();
        List<int> primary = new List<int>();
        List<TileRegion> climbs = new List<TileRegion>();
        bool vertical = s.Profile == WorldProfile.Ascent || s.Profile == WorldProfile.Descent;
        if (vertical && (h <= w || h < 72)) throw new ArgumentException("Vertical profiles need Height > Width and at least 72 cells.");
        int y0 = s.Profile == WorldProfile.Descent ? h - 8 : s.Profile == WorldProfile.Mixed ? h / 2 : 8;
        int delta1 = 4 + (int)(Mix(unchecked((uint)seed)) % 3);
        int delta2 = -delta1;
        if (s.Profile == WorldProfile.Mixed) { if ((seed & 1) != 0) delta1 = -delta1; delta2 = -delta1 / 2; }
        int current = AddNode(nodes, 8, y0);
        Rest(links, primary, current); // Legal elapsed recovery at Spawn, never an automatic refill.
        current = Add(nodes, links, primary, current, 11, y0, TraversalAction.Sprint);
        int firstLaunch = current;
        current = Add(nodes, links, primary, current, 14, y0, TraversalAction.Jump);
        current = Add(nodes, links, primary, current, 16, y0, TraversalAction.Walk);
        int firstRest = current;
        Rest(links, primary, current);
        current = Add(nodes, links, primary, current, 20, y0, TraversalAction.Crawl);
        Rest(links, primary, current);
        if (vertical)
        {
            int rise = s.Profile == WorldProfile.Ascent ? 8 : -8;
            for (int step = 0; step < 8; step++)
            {
                TraversalNode at = nodes[current];
                current = Add(nodes, links, primary, current, at.FloorX + 2, at.FloorY + rise, TraversalAction.Climb);
                Rest(links, primary, current);
                if (step == 2)
                {
                    int x = nodes[current].FloorX, y = nodes[current].FloorY;
                    current = Add(nodes, links, primary, current, x + 3, y, TraversalAction.Sprint);
                    current = Add(nodes, links, primary, current, x + 8, y, TraversalAction.DoubleJump);
                    current = Add(nodes, links, primary, current, x + 10, y, TraversalAction.Walk);
                    Rest(links, primary, current);
                }
            }
        }
        else
        {
            current = Add(nodes, links, primary, current, 23, y0, TraversalAction.Walk);
            current = Add(nodes, links, primary, current, 25, y0 + delta1, TraversalAction.Climb);
            Rest(links, primary, current);
            current = Add(nodes, links, primary, current, 28, y0 + delta1, TraversalAction.Sprint);
            current = Add(nodes, links, primary, current, 33, y0 + delta1, TraversalAction.DoubleJump);
            current = Add(nodes, links, primary, current, 36, y0 + delta1, TraversalAction.Walk);
            Rest(links, primary, current);
            current = Add(nodes, links, primary, current, 40, y0 + delta1, TraversalAction.Crawl);
            Rest(links, primary, current);
            current = Add(nodes, links, primary, current, 43, y0 + delta1, TraversalAction.Walk);
            current = Add(nodes, links, primary, current, 45, y0 + delta1 + delta2, TraversalAction.Climb);
            current = Add(nodes, links, primary, current, Math.Min(w - 5, 51 + (int)(Mix(unchecked((uint)seed + 4u)) % 3)), y0 + delta1 + delta2, TraversalAction.Walk);
        }

        links.Add(new TraversalLink { FromNode = firstLaunch, ToNode = firstRest, Action = TraversalAction.DoubleJump, Primary = false, BranchId = 1 });
        int branchLanding = AddNode(nodes, 6, y0 - 4);
        int deadEnd = AddNode(nodes, 2, y0 - 4);
        links.Add(new TraversalLink { FromNode = 0, ToNode = branchLanding, Action = TraversalAction.Climb, Primary = false, BranchId = 2 });
        links.Add(new TraversalLink { FromNode = branchLanding, ToNode = deadEnd, Action = TraversalAction.Walk, Primary = false, BranchId = 2 });
        links.Add(new TraversalLink { FromNode = deadEnd, ToNode = branchLanding, Action = TraversalAction.Walk, Primary = false, BranchId = 2 });
        links.Add(new TraversalLink { FromNode = branchLanding, ToNode = 0, Action = TraversalAction.Climb, Primary = false, BranchId = 2 });
        world.Route = new TraversalPlan { Profile = s.Profile, Nodes = nodes.ToArray(), Links = links.ToArray(), PrimaryLinks = primary.ToArray() };
        world.SpawnX = nodes[0].FloorX; world.SpawnY = nodes[0].FloorY + 1;
        world.GoalX = nodes[current].FloorX; world.GoalY = nodes[current].FloorY + 1;
        List<TileRegion> voids = new List<TileRegion>();
        foreach (TraversalLink link in world.Route.Links)
        {
            TraversalNode a = nodes[link.FromNode], b = nodes[link.ToNode];
            if (link.Action == TraversalAction.Climb)
            {
                int direction = Math.Sign(b.FloorX - a.FloorX);
                double wall = (b.FloorY > a.FloorY ? b.FloorX - direction * .5 : a.FloorX + direction * .5) * world.CellSize;
                link.ClimbX = wall + (b.FloorY > a.FloorY ? -direction * .325 : direction * .325);
                climbs.Add(new TileRegion(Math.Min(a.FloorX, b.FloorX) - 1, Math.Min(a.FloorY, b.FloorY) + 1, Math.Max(a.FloorX, b.FloorX) + 1, Math.Max(a.FloorY, b.FloorY) + 3));
            }
            link.Path = TraversalGeometry.CreatePath(world, link, caps);
            link.Duration = link.Action == TraversalAction.Rest ? 10.0 : link.Path[link.Path.Length - 1].Time;
            // Seal each traversable envelope before carving. Natural caves remain outside its protective shell.
            foreach (TrajectorySample p in link.Path)
                Rectangle(world, (int)Math.Floor(p.X / .5) - 2, (int)Math.Floor((p.Y + caps.SoleLocalY) / .5) - 2,
                    (int)Math.Ceiling(p.X / .5) + 2, (int)Math.Ceiling((p.Y + caps.SoleLocalY + caps.StandingHeight) / .5) + 2, 1);
            if (link.Primary && (link.Action == TraversalAction.Jump || link.Action == TraversalAction.DoubleJump))
            {
                // A single half-unit opening can support the compound body across its corners.
                int landing = b.FloorX - (link.Action == TraversalAction.Jump ? 0 : 2);
                voids.Add(new TileRegion(a.FloorX + 1, 1, landing - 1, a.FloorY - 1) { CueX = a.FloorX, CueY = a.FloorY + 1 });
            }
        }
        foreach (TraversalLink link in world.Route.Links) CarvePath(world, link, caps);
        foreach (TraversalLink link in world.Route.Links) Support(world, link);
        foreach (TraversalLink link in world.Route.Links)
            if (link.Action == TraversalAction.Crawl)
            {
                TraversalNode a = nodes[link.FromNode];
                Rectangle(world, a.FloorX + 2, a.FloorY + 2, a.FloorX + 2, a.FloorY + 3, 1);
            }
        foreach (TileRegion region in voids) Rectangle(world, region.MinX, region.MinY, region.MaxX, region.MaxY + 1, 0);
        world.ClimbRegions = climbs.ToArray(); world.VoidRegions = voids.ToArray();
        return world;
    }

    private static int AddNode(List<TraversalNode> nodes, int x, int y) { nodes.Add(new TraversalNode(x, y)); return nodes.Count - 1; }
    private static int Add(List<TraversalNode> nodes, List<TraversalLink> links, List<int> primary, int from, int x, int y, TraversalAction action)
    {
        int to = AddNode(nodes, x, y); primary.Add(links.Count);
        links.Add(new TraversalLink { FromNode = from, ToNode = to, Action = action }); return to;
    }
    private static void Rest(List<TraversalLink> links, List<int> primary, int node)
    { primary.Add(links.Count); links.Add(new TraversalLink { FromNode = node, ToNode = node, Action = TraversalAction.Rest, Duration = 10 }); }

    private static void Rectangle(WorldData world, int minX, int minY, int maxX, int maxY, int value)
    {
        for (int x = Math.Max(1, minX); x <= Math.Min(world.Tiles.GetLength(0) - 2, maxX); x++)
            for (int y = Math.Max(1, minY); y <= Math.Min(world.Tiles.GetLength(1) - 2, maxY); y++) world.Tiles[x, y] = value;
    }
    private static void CarvePath(WorldData world, TraversalLink link, TraversalCapabilities caps)
    {
        double bodyHeight = link.Action == TraversalAction.Crawl ? caps.CrouchedHeight : caps.StandingHeight;
        for (int i = 1; i < link.Path.Length; i++)
        {
            TrajectorySample a = link.Path[i - 1], b = link.Path[i];
            double bottom = Math.Min(a.Y, b.Y) + caps.SoleLocalY;
            double top = Math.Max(a.Y, b.Y) + caps.SoleLocalY + bodyHeight + .025;
            double left = Math.Min(a.X, b.X) - .325, right = Math.Max(a.X, b.X) + .325;
            for (int x = 1; x < world.Tiles.GetLength(0) - 1; x++) for (int y = 1; y < world.Tiles.GetLength(1) - 1; y++)
                if ((x + .5) * .5 > left + .00001 && (x - .5) * .5 < right - .00001 &&
                    (y + .5) * .5 > bottom + .00001 && (y - .5) * .5 < top - .00001) world.Tiles[x, y] = 0;
        }
    }
    private static void Floor(WorldData world, int from, int to, int y) { Rectangle(world, from, y - 1, to, y, 1); }
    private static void Support(WorldData world, TraversalLink link)
    {
        if (!link.Primary && link.Action == TraversalAction.DoubleJump) return;
        TraversalNode a = world.Route.Nodes[link.FromNode], b = world.Route.Nodes[link.ToNode];
        if (link.Action == TraversalAction.Jump || link.Action == TraversalAction.DoubleJump)
        { Floor(world, a.FloorX - 3, a.FloorX, a.FloorY); Floor(world, b.FloorX - (link.Action == TraversalAction.Jump ? 0 : 2), b.FloorX + 2, b.FloorY); }
        else if (link.Action == TraversalAction.Climb)
        {
            int direction = Math.Sign(b.FloorX - a.FloorX);
            if (b.FloorY > a.FloorY)
            {
                Floor(world, Math.Min(a.FloorX, b.FloorX - direction), Math.Max(a.FloorX, b.FloorX - direction), a.FloorY);
                Rectangle(world, Math.Min(b.FloorX, b.FloorX + direction * 2), a.FloorY, Math.Max(b.FloorX, b.FloorX + direction * 2), b.FloorY, 1);
            }
            else
            {
                Rectangle(world, Math.Min(a.FloorX - direction * 2, a.FloorX), b.FloorY, Math.Max(a.FloorX - direction * 2, a.FloorX), a.FloorY, 1);
                Floor(world, Math.Min(a.FloorX + direction, b.FloorX), Math.Max(a.FloorX + direction, b.FloorX), b.FloorY);
            }
        }
        else Floor(world, Math.Min(a.FloorX, b.FloorX), Math.Max(a.FloorX, b.FloorX), a.FloorY);
    }
}

internal static class TraversalGeometry
{
    public static double RootY(WorldData w, TraversalNode n, TraversalCapabilities c) { return (n.FloorY + .5) * w.CellSize - c.SoleLocalY + .04; }
    public static TrajectorySample[] CreatePath(WorldData w, TraversalLink link, TraversalCapabilities c)
    {
        TraversalNode a = w.Route.Nodes[link.FromNode], b = w.Route.Nodes[link.ToNode];
        double x = a.FloorX * w.CellSize, y = RootY(w, a, c), targetX = b.FloorX * w.CellSize, targetY = RootY(w, b, c), time = 0;
        List<TrajectorySample> path = new List<TrajectorySample>(); path.Add(new TrajectorySample(0, x, y));
        if (link.Action == TraversalAction.Jump || link.Action == TraversalAction.DoubleJump)
        {
            TrajectorySample[] trace = link.Action == TraversalAction.Jump ? c.SingleJumpSamples : c.DoubleJumpSamples;
            for (int i = 1; i < trace.Length; i++) path.Add(new TrajectorySample(trace[i].Time * 1.1, x + trace[i].X, y + trace[i].Y));
            TrajectorySample end = path[path.Count - 1]; time = end.Time + Math.Abs(targetX - end.X) / c.WalkSpeed * 1.15;
            path.Add(new TrajectorySample(time, targetX, targetY));
        }
        else if (link.Action == TraversalAction.Climb)
        {
            time += Math.Abs(link.ClimbX - x) / c.WalkSpeed * 1.15;
            path.Add(new TrajectorySample(time, link.ClimbX, y));
            if (targetY > y)
            {
                // .8u is below the physically measured 1.01u capture limit, with margin.
                const double grabRise = .8;
                time += Math.Max(0, targetY - y - grabRise) / c.ClimbSpeed * 1.2;
                path.Add(new TrajectorySample(time, link.ClimbX, targetY - grabRise));
                double pullTime = c.PullUpSeconds * 1.15 + .04;
                double verticalPart = grabRise / (grabRise + Math.Abs(targetX - link.ClimbX));
                time += pullTime * verticalPart;
                path.Add(new TrajectorySample(time, link.ClimbX, targetY));
                time += pullTime * (1 - verticalPart);
                path.Add(new TrajectorySample(time, targetX, targetY));
            }
            else
            {
                time += Math.Abs(targetY - y) / c.ClimbSpeed * 1.2;
                path.Add(new TrajectorySample(time, link.ClimbX, targetY));
                time += Math.Abs(targetX - link.ClimbX) / c.WalkSpeed * 1.15;
                path.Add(new TrajectorySample(time, targetX, targetY));
            }
        }
        else
        {
            double speed = link.Action == TraversalAction.Crawl ? c.CrawlSpeed : link.Action == TraversalAction.Sprint ? c.SprintSpeed : c.WalkSpeed;
            time = link.Action == TraversalAction.Rest ? 10 : Math.Abs(targetX - x) / speed * 1.15;
            path.Add(new TrajectorySample(time, targetX, targetY));
        }
        return path.ToArray();
    }
}
