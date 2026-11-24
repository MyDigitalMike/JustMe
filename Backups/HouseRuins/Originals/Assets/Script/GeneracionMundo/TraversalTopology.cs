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
    readonly HashSet<int> stairPairs = new HashSet<int>();
    int previousStair = -1;
    TraversalAction previousFeature = TraversalAction.Rest;
    int runVerticalBias;
    bool hasRunClimb;
    readonly List<int> roomRoots = new List<int>();
    int branchId, explorationNode = -1, roomNumber;

    public static WorldData Build(TraversalGenerationSettings settings, TraversalCapabilities caps, int seed, int attempt)
    {
        var p = new TraversalTopology { random = unchecked((uint)seed + (uint)attempt * 0x9e3779b9u) };
        TraversalLayout layout = settings.Layout == TraversalLayout.Random ? TraversalLayout.Exploration : settings.Layout;
        p.runVerticalBias = layout == TraversalLayout.Ascent ? 1 : layout == TraversalLayout.Descent ? -1 : 0;
        p.nodes.Add(new TraversalNode(0, 0)); p.Rest();
        int rise = 20 + p.Next(5);
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
                p.Run(1, 1); p.Stairs(-1, 1, rise); p.Run(1, 1); p.Stairs(1, 1, rise); break;
            case TraversalLayout.Switchback:
                p.Run(1, 2); p.Stairs(1, 1, 8); p.Turn(-1); p.Run(-1, 2);
                p.Stairs(1, -1, 8); p.Turn(1); p.Run(1, 2); break;
            case TraversalLayout.Cross:
                p.Run(1, 2);
                p.Excursion(1, -1, 10); p.Excursion(-1, 1, 10);
                p.Turn(1); p.Run(1, 2); break;
            case TraversalLayout.Exploration:
                // Preserve the section seed stream of the approved mixed examples.
                p.Next(4); profile = WorldProfile.Mixed;
                p.Explore(profile); break;
            default: throw new ArgumentException("Unsupported macro layout.");
        }
        if (p.firstLaunch >= 0) p.links.Add(new TraversalLink { FromNode = p.firstLaunch, ToNode = p.firstLanding, Action = TraversalAction.DoubleJump, Primary = false, BranchId = 1 });
        int goal = p.at;
        if (!p.AddDeadEnds()) throw new TraversalGenerationException(seed, profile, attempt, "No room for two returning dead ends.");
        p.at = goal;
        bool mirror = (seed & 1) != 0;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (TraversalNode n in p.nodes)
        {
            if (mirror) n.FloorX = -n.FloorX;
            minX = Math.Min(minX, n.FloorX); maxX = Math.Max(maxX, n.FloorX);
            minY = Math.Min(minY, n.FloorY); maxY = Math.Max(maxY, n.FloorY);
        }
        const int marginX = 10, marginY = 22;
        int width = Math.Max(56, maxX - minX + marginX * 2 + 1), height = Math.Max(34, maxY - minY + marginY * 2 + 1);
        if (width > 512 || height > 512) throw new TraversalGenerationException(seed, profile, attempt, "Route exceeds bounded terrain storage.");
        foreach (TraversalNode n in p.nodes) { n.FloorX += marginX - minX; n.FloorY += marginY - minY; }
        var world = new WorldData { Seed = seed, CellSize = settings.CellSize, Tiles = new int[width, height], Surface = new int[width] };
        world.ExplorationNode = p.explorationNode;
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
        if (branchId == 0) primary.Add(links.Count);
        links.Add(new TraversalLink { FromNode = at, ToNode = next, Action = action, Primary = branchId == 0, BranchId = branchId }); at = next;
    }
    void Rest() { Link(at, TraversalAction.Rest); }
    void Run(int direction, int count)
    {
        PrepareHeading(direction);
        Room(direction, 10 + Next(7));
        // A distance budget, not N copies of a compulsory three-obstacle recipe.
        int remaining = count * (24 + Next(11));
        int nextRoom = remaining - 18 - Next(15);
        while (remaining > 0)
        {
            if (remaining <= nextRoom && remaining >= 12)
            {
                int length = Math.Min(remaining, 12 + Next(11)); Room(direction, length);
                remaining -= length; nextRoom = remaining - 18 - Next(15);
                continue;
            }
            int roomLength = Math.Min(remaining, 2 + Next(7));
            Step(direction * roomLength, 0, TraversalAction.Walk); remaining -= roomLength;
            int choice = Next(100);
            TraversalAction action = firstLaunch < 0 ? TraversalAction.Jump : choice < 35 ? TraversalAction.Walk :
                choice < 60 ? TraversalAction.Jump : choice < 72 ? TraversalAction.Crawl :
                choice < 85 ? TraversalAction.DoubleJump : TraversalAction.Climb;
            if (action == previousFeature) action = TraversalAction.Walk;
            int cost = action == TraversalAction.Climb || action == TraversalAction.Crawl ? 4 : action == TraversalAction.Jump ? 8 : 11;
            previousFeature = action;
            if (action == TraversalAction.Walk || cost > remaining) continue;
            if (action == TraversalAction.Climb)
            {
                int oldX = nodes[at].FloorX;
                RunClimb(direction); remaining -= Math.Abs(nodes[at].FloorX - oldX); continue;
            }
            if (action == TraversalAction.Crawl) Step(direction * 4, 0, action);
            else
            {
                Step(direction * 3, 0, TraversalAction.Sprint); int launch = at;
                Step(direction * (action == TraversalAction.Jump ? 3 : 5), 0, action);
                Step(direction * (action == TraversalAction.Jump ? 2 : 3), 0, TraversalAction.Walk);
                if (firstLaunch < 0 && action == TraversalAction.Jump) { firstLaunch = launch; firstLanding = at; }
            }
            remaining -= cost; Rest();
        }
        if (!hasRunClimb) RunClimb(direction);
        // Keep the next macro junction clear of the preceding rope/landing.
        Step(direction * 6, 0, TraversalAction.Walk); Rest();
    }
    void RunClimb(int direction)
    {
        int vertical = runVerticalBias != 0 ? runVerticalBias : Next(2) == 0 ? -1 : 1;
        Stair(vertical, direction, true); hasRunClimb = true;
    }
    void Turn(int direction)
    {
        if (!NeedsTurn(direction)) return;
        // Cross the open shaft by a real jump; do not floor over a climb exit.
        Step(-direction * 3, 0, TraversalAction.Walk); Rest();
        Step(direction * 3, 0, TraversalAction.Sprint); Step(direction * 3, 0, TraversalAction.Jump);
        links[links.Count - 1].LethalGap = false; // Reachable lower landing, not a designated lethal void.
        Step(direction * 2, 0, TraversalAction.Walk); Rest();
    }
    void Stair(int vertical, int horizontal, bool junction)
    {
        int start = Next(9), chosen = -1;
        for (int pass = 0; pass < 2 && chosen < 0; pass++)
            for (int i = 0; i < 9; i++)
            {
                int key = (start + i) % 9;
                if (key == previousStair || key / 3 == 0 || (junction && key / 3 != 2)) continue;
                if (pass == 0 && previousStair >= 0 && stairPairs.Contains(previousStair * 9 + key)) continue;
                chosen = key; break;
            }
        if (previousStair >= 0) stairPairs.Add(previousStair * 9 + chosen);
        previousStair = chosen;
        Step(horizontal * (2 + chosen % 3), vertical * (6 + chosen / 3), TraversalAction.Climb); Rest();
    }
    void Stairs(int vertical, int horizontal, int count)
    {
        int remaining = count;
        while (remaining > 0)
        {
            int bank = Math.Min(remaining, 2 + Next(3));
            for (int i = 0; i < bank; i++) Stair(vertical, horizontal, i == 0 || i == bank - 1);
            remaining -= bank;
            if (remaining > 0) Room(horizontal, 12 + Next(15));
        }
    }
    void Excursion(int vertical, int horizontal, int count)
    {
        int begin = links.Count;
        Stairs(vertical, horizontal, count);
        Room(horizontal, 6 + Next(5));
        int end = links.Count;
        for (int i = end - 1; i >= begin; i--)
        {
            TraversalLink outbound = links[i];
            Link(outbound.FromNode, outbound.Action); links[links.Count - 1].ChamberHeight = outbound.ChamberHeight;
        }
        Rest();
        if (vertical > 0) Room(-horizontal, 6 + Next(5));
    }
    bool NeedsTurn(int direction)
    {
        for (int i = links.Count - 1; i >= 0; i--)
        {
            TraversalLink link = links[i]; if (link.Action == TraversalAction.Rest) continue;
            TraversalNode a = nodes[link.FromNode], b = nodes[link.ToNode];
            return link.Action == TraversalAction.Climb && b.FloorY > a.FloorY && Math.Sign(b.FloorX - a.FloorX) != direction;
        }
        return false;
    }
    void PrepareHeading(int direction) { if (NeedsTurn(direction)) Turn(direction); }
    void Room(int direction, int width)
    {
        PrepareHeading(direction);
        // A room is planned independently of the obstacle trace; alternate available shapes,
        // but sample their dimensions and locations separately for every world.
        int style = roomNumber < 2 ? roomNumber++ : Next(2);
        double ceiling = style == 0 ? 5 + Next(9) * .5 : 0;
        Step(direction * (width / 2), 0, TraversalAction.Walk); links[links.Count - 1].ChamberHeight = ceiling;
        if (branchId == 0) roomRoots.Add(at);
        Rest(); links[links.Count - 1].ChamberHeight = ceiling;
        Step(direction * (width - width / 2), 0, TraversalAction.Walk); links[links.Count - 1].ChamberHeight = ceiling;
        Rest(); links[links.Count - 1].ChamberHeight = ceiling;
    }
    void Explore(WorldProfile profile)
    {
        // Seeded section grammar rather than selecting one of six whole-world recipes.
        int vertical = profile == WorldProfile.Descent ? -1 : profile == WorldProfile.Ascent ? 1 : Next(2) * 2 - 1;
        if (Next(2) == 0) Run(1, 1); else Room(1, 10 + Next(9));
        int sections = 5 + Next(3), heading = 1;
        bool hasHorizontalTurn = false;
        int ascent = 0, descent = 0;
        for (int i = 0; i < sections; i++)
        {
            if (profile == WorldProfile.Mixed || profile == WorldProfile.Horizontal) vertical = Next(2) * 2 - 1;
            else if (i == 2) vertical = -vertical;
            else vertical = profile == WorldProfile.Descent ? -1 : 1;
            if (i >= sections - 2 && ascent < 14) vertical = 1;
            else if (i >= sections - 2 && descent < 14) vertical = -1;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (TraversalNode n in nodes) { minY = Math.Min(minY, n.FloorY); maxY = Math.Max(maxY, n.FloorY); }
            int startY = nodes[at].FloorY;
            Stairs(vertical, heading, profile == WorldProfile.Horizontal ? 1 + Next(2) : 2 + Next(3));
            ascent += Math.Max(0, nodes[at].FloorY - startY); descent += Math.Max(0, startY - nodes[at].FloorY);
            // Change heading at the new landing, outside the earlier height band.
            // Reversing before the shaft would drill through the floor just traversed.
            bool outside = vertical > 0 ? nodes[at].FloorY >= maxY + 7 : nodes[at].FloorY <= minY - 7;
            if (outside && (Next(3) == 0 || !hasHorizontalTurn))
            { heading = -heading; hasHorizontalTurn = true; }
            if (Next(3) == 0) Run(heading, 1); else Room(heading, 12 + Next(17));
        }
        if (firstLaunch < 0) Run(heading, 1);
    }
    bool AddDeadEnds()
    {
        int target = 2 + Next(2), count = 0;
        var used = new HashSet<int>();
        int start = Next(roomRoots.Count);
        for (int k = 0; k < roomRoots.Count * 3 && count < target; k++)
        {
            int root = roomRoots[(start + k) % roomRoots.Count];
            if (used.Contains(root)) continue;
            // Terminal must sit above even the tallest ordinary chamber, not form a
            // balcony that reconnects to its roof. Intermediate landings remain reachable.
            int direction = Next(2) * 2 - 1, steps = 3 + Next(2), length = 4 + Next(8);
            TraversalNode n = nodes[root];
            int endX = n.FloorX + direction * (steps * 3 + length);
            int low = Math.Min(n.FloorX, endX) - 2, high = Math.Max(n.FloorX, endX) + 2;
            bool free = true;
            foreach (TraversalLink link in links)
            {
                if (link.Action == TraversalAction.Rest) continue;
                TraversalNode a = nodes[link.FromNode], b = nodes[link.ToNode];
                if (Math.Max(a.FloorX, b.FloorX) >= low && Math.Min(a.FloorX, b.FloorX) <= high &&
                    Math.Max(a.FloorY, b.FloorY) > n.FloorY && Math.Min(a.FloorY, b.FloorY) < n.FloorY + steps * 8 + 22)
                { free = false; break; }
            }
            if (!free) continue;
            used.Add(root);
            at = root; branchId = 2 + count;
            int begin = links.Count;
            for (int j = 0; j < steps; j++) { Step(direction * 3, 8, TraversalAction.Climb); Rest(); }
            Room(direction, length);
            if (count == 0) explorationNode = at;
            int end = links.Count;
            for (int i = end - 1; i >= begin; i--)
            {
                TraversalLink outbound = links[i];
                Link(outbound.FromNode, outbound.Action); links[links.Count - 1].ChamberHeight = outbound.ChamberHeight;
            }
            Rest(); branchId = 0; count++;
        }
        return count >= 2;
    }
}
