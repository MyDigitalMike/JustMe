using System;
using System.Collections.Generic;

// A constrained full itinerary avoids an exponential search over page subsets and stamina.
public static class DiaryPlanner
{
    public static string Attach(WorldData world, int required)
    {
        if (required < 1 || required > 12) return "Required fragment count must be 1..12.";
        if (world == null || world.Route == null) return "Missing traversal plan for diary placement.";
        TraversalPlan plan = world.Route;
        int[] branch = ExplorationLinks(plan);
        if (branch.Length != 4 || plan.Links[branch[0]].FromNode != 0 || plan.Links[branch[3]].ToNode != 0)
            return "Diary exploration needs the directed dead-end return at Spawn.";
        int deadEnd = plan.Links[branch[1]].ToNode;
        var eligible = new List<int>();
        var positions = new HashSet<long> { PositionKey(plan.Nodes[0]), PositionKey(plan.Nodes[deadEnd]) };
        foreach (int index in plan.PrimaryLinks)
        {
            int nodeIndex = plan.Links[index].ToNode;
            TraversalNode node = plan.Nodes[nodeIndex];
            if (node.FloorX == world.GoalX && node.FloorY + 1 == world.GoalY) continue;
            if (positions.Add(PositionKey(node))) eligible.Add(nodeIndex);
        }
        if (eligible.Count < required - 1) return "Not enough distinct main-route anchors for the requested fragments.";
        world.RequiredFragments = required;
        world.FragmentNodes = new int[required]; world.FragmentNodes[0] = deadEnd;
        int mainCount = required - 1;
        for (int i = 0; i < mainCount; i++)
        {
            int first = i * eligible.Count / mainCount, end = (i + 1) * eligible.Count / mainCount;
            uint choice = Mix(unchecked((uint)world.Seed + (uint)i * 0x9e3779b9u));
            world.FragmentNodes[i + 1] = eligible[first + (int)(choice % (uint)(end - first))];
        }
        world.CollectionLinks = new int[branch.Length + plan.PrimaryLinks.Length];
        Array.Copy(branch, world.CollectionLinks, branch.Length);
        Array.Copy(plan.PrimaryLinks, 0, world.CollectionLinks, branch.Length, plan.PrimaryLinks.Length);
        return null;
    }

    public static long PositionKey(TraversalNode node) { return ((long)node.FloorX << 32) | (uint)node.FloorY; }
    public static int[] ExplorationLinks(TraversalPlan plan)
    {
        var branch = new List<int>();
        for (int i = 0; i < plan.Links.Length; i++)
            if (!plan.Links[i].Primary && plan.Links[i].BranchId == 2) branch.Add(i);
        return branch.ToArray();
    }
    static uint Mix(uint x)
    {
        unchecked { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16); }
    }
}
