using System;
using System.Collections.Generic;

// Macro topology only: keep gravity upright and reuse calibrated traversal actions.
internal sealed class TraversalTopology
{
    readonly List<TraversalNode> nodes = new List<TraversalNode>();
    readonly List<TraversalLink> links = new List<TraversalLink>();
    readonly List<int> primary = new List<int>();
    int at, firstLaunch = -1, firstLanding;
    uint random;

    public static WorldData Build(TraversalGenerationSettings settings, TraversalCapabilities caps, int seed, int attempt)
    {
        var p = new TraversalTopology { random = unchecked((uint)seed + (uint)attempt * 0x9e3779b9u) };
        TraversalLayout layout = settings.Layout == TraversalLayout.Random ? (TraversalLayout)(2 + p.Next(6)) : settings.Layout;
        p.nodes.Add(new TraversalNode(0, 0)); p.Rest();
        int rise = 16 + p.Next(7);
        WorldProfile profile = WorldProfile.Mixed;
        switch (layout)
        {
            case TraversalLayout.Horizontal:
                profile = WorldProfile.Horizontal;
                p.Run(1, 4 + p.Next(3)); break;
            case TraversalLayout.Ascent:
            case TraversalLayout.Descent:
                profile = layout == TraversalLayout.Ascent ? WorldProfile.Ascent : WorldProfile.Descent;
                if (layout == TraversalLayout.Descent) { p.Step(4 + p.Next(3), 0, TraversalAction.Walk); p.Rest(); }
                p.Stairs(layout == TraversalLayout.Ascent ? 1 : -1, 1, rise / 2);
                p.Run(1, 1); p.Stairs(layout == TraversalLayout.Ascent ? 1 : -1, 1, rise - rise / 2); p.Run(1, 1); break;
            case TraversalLayout.U:
                p.Run(1, 1); p.Stairs(-1, 1, rise); p.Run(1, 3); p.Stairs(1, 1, rise); break;
            case TraversalLayout.Switchback:
                p.Run(1, 2); p.Stairs(1, 1, 8); p.Turn(-1); p.Run(-1, 2);
                p.Stairs(1, -1, 8); p.Turn(1); p.Run(1, 2); break;
            case TraversalLayout.Cross:
                p.Run(1, 2);
                p.Excursion(1, -1, 10); p.Excursion(-1, 1, 10);
                p.Turn(1); p.Run(1, 2); break;
            default: throw new ArgumentException("Unsupported macro layout.");
        }
        p.links.Add(new TraversalLink { FromNode = p.firstLaunch, ToNode = p.firstLanding, Action = TraversalAction.DoubleJump, Primary = false, BranchId = 1 });
        int landing = p.nodes.Count; p.nodes.Add(new TraversalNode(-2, -4));
        int deadEnd = p.nodes.Count; p.nodes.Add(new TraversalNode(-6, -4));
        p.Branch(0, landing, TraversalAction.Climb); p.Branch(landing, deadEnd, TraversalAction.Walk);
        p.Branch(deadEnd, landing, TraversalAction.Walk); p.Branch(landing, 0, TraversalAction.Climb);
        bool mirror = (seed & 1) != 0;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (TraversalNode n in p.nodes)
        {
            if (mirror) n.FloorX = -n.FloorX;
            minX = Math.Min(minX, n.FloorX); maxX = Math.Max(maxX, n.FloorX);
            minY = Math.Min(minY, n.FloorY); maxY = Math.Max(maxY, n.FloorY);
        }
        const int margin = 10;
        int width = Math.Max(56, maxX - minX + margin * 2 + 1), height = Math.Max(34, maxY - minY + margin * 2 + 1);
        if (width > 384 || height > 384) throw new ArgumentException("Macro route exceeds bounded terrain storage.");
        foreach (TraversalNode n in p.nodes) { n.FloorX += margin - minX; n.FloorY += margin - minY; }
        var world = new WorldData { Seed = seed, CellSize = settings.CellSize, Tiles = new int[width, height], Surface = new int[width] };
        for (int x = 0; x < width; x++)
        {
            world.Surface[x] = height - 3;
            for (int y = 0; y < height; y++) world.Tiles[x, y] = 1;
        }
        world.Route = new TraversalPlan { Profile = profile, Layout = layout, Nodes = p.nodes.ToArray(), Links = p.links.ToArray(), PrimaryLinks = p.primary.ToArray() };
        world.SpawnX = p.nodes[0].FloorX; world.SpawnY = p.nodes[0].FloorY + 1;
        world.GoalX = p.nodes[p.at].FloorX; world.GoalY = p.nodes[p.at].FloorY + 1;
        HardcoreWorldGenerator.Rasterize(world, caps, 2.5);
        return world;
    }

    int Next(int bound)
    {
        unchecked { random += 0x9e3779b9u; uint x = random; x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; return (int)(x % (uint)bound); }
    }
    void Step(int dx, int dy, TraversalAction action)
    {
        TraversalNode start = nodes[at]; int next = nodes.Count;
        nodes.Add(new TraversalNode(start.FloorX + dx, start.FloorY + dy)); Link(next, action);
    }
    void Link(int next, TraversalAction action)
    {
        primary.Add(links.Count); links.Add(new TraversalLink { FromNode = at, ToNode = next, Action = action }); at = next;
    }
    void Rest() { Link(at, TraversalAction.Rest); }
    void Branch(int from, int to, TraversalAction action)
    { links.Add(new TraversalLink { FromNode = from, ToNode = to, Action = action, Primary = false, BranchId = 2 }); }
    void Run(int direction, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Step(direction * 3, 0, TraversalAction.Sprint); int launch = at;
            Step(direction * 3, 0, TraversalAction.Jump); Step(direction * 2, 0, TraversalAction.Walk);
            if (firstLaunch < 0) { firstLaunch = launch; firstLanding = at; }
            Rest(); Step(direction * 4, 0, TraversalAction.Crawl); Rest();
            Step(direction * (3 + Next(3)), 0, TraversalAction.Walk);
            Step(direction * 3, 0, TraversalAction.Sprint); Step(direction * 5, 0, TraversalAction.DoubleJump);
            Step(direction * 3, 0, TraversalAction.Walk); Rest();
        }
    }
    void Turn(int direction)
    {
        // Cross the open shaft by a real jump; do not floor over a climb exit.
        Step(-direction * 3, 0, TraversalAction.Walk); Rest();
        Step(direction * 3, 0, TraversalAction.Sprint); Step(direction * 3, 0, TraversalAction.Jump);
        links[links.Count - 1].LethalGap = false; // Reachable lower landing, not a designated lethal void.
        Step(direction * 2, 0, TraversalAction.Walk); Rest();
    }
    void Stairs(int vertical, int horizontal, int count)
    { for (int i = 0; i < count; i++) { Step(horizontal * 2, vertical * 8, TraversalAction.Climb); Rest(); } }
    void Excursion(int vertical, int horizontal, int count)
    {
        var returning = new List<int>();
        for (int i = 0; i < count; i++) { returning.Add(at); Stairs(vertical, horizontal, 1); }
        for (int i = returning.Count - 1; i >= 0; i--) { Link(returning[i], TraversalAction.Climb); Rest(); }
    }
}
