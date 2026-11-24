using System;
using System.Collections.Generic;

public sealed class TraversalValidation
{
    public bool Valid;
    public string Reason;
    public int FailedLink = -1;
    public int FragmentsReached;
    public double RemainingStamina, TotalSeconds;
}

// Conservative acceptance model, not a substitute for physical replay.
public static class TraversalValidator
{
    public static void ValidateCapabilities(TraversalCapabilities c)
    {
        if (c == null || !Positive(c.StandingWidth) || !Positive(c.StandingHeight) || !Positive(c.CrouchedHeight) ||
            c.CrouchedHeight >= c.StandingHeight || !Positive(c.WalkSpeed) || !Positive(c.SprintSpeed) ||
            !Positive(c.CrawlSpeed) || !Positive(c.ClimbSpeed) || !Positive(c.FixedStep) || c.FixedStep > .05 ||
            c.SoleLocalY >= 0 || !Positive(c.SingleJumpPeak) || !Positive(c.DoubleJumpPeak) ||
            c.SingleJumpSamples == null || c.SingleJumpSamples.Length < 4 || c.DoubleJumpSamples == null || c.DoubleJumpSamples.Length < 4)
            throw new ArgumentException("Measured traversal capabilities are incomplete.");
        foreach (TrajectorySample[] trace in new[] { c.SingleJumpSamples, c.DoubleJumpSamples })
        {
            double previous = -1;
            foreach (TrajectorySample p in trace)
            {
                if (p == null || !Finite(p.Time) || !Finite(p.X) || !Finite(p.Y) || p.Time <= previous)
                    throw new ArgumentException("Jump samples must be finite and ordered.");
                previous = p.Time;
            }
        }
    }
    static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    static bool Positive(double x) { return Finite(x) && x > 0; }

    public static TraversalValidation Validate(WorldData world, TraversalCapabilities caps, StaminaSettings settings, int requiredFragments = -1)
    {
        ValidateCapabilities(caps); settings.Validate();
        var result = new TraversalValidation();
        if (world == null || world.Route == null || world.Tiles == null || world.Route.Nodes == null || world.Route.Links == null || world.Route.PrimaryLinks == null)
            return Fail(result, -1, "Missing world or directed primary route.");
        if (world.CellSize != .5 || world.Route.PrimaryLinks.Length < 5) return Fail(result, -1, "Missing hard traversal geometry.");
        if (world.Route.Layout == TraversalLayout.Exploration)
        {
            int left = 0, right = 0, up = 0, down = 0;
            foreach (int index in world.Route.PrimaryLinks)
            {
                if (index < 0 || index >= world.Route.Links.Length) return Fail(result, index, "Invalid link index.");
                TraversalLink link = world.Route.Links[index];
                if (link.FromNode < 0 || link.ToNode < 0 || link.FromNode >= world.Route.Nodes.Length || link.ToNode >= world.Route.Nodes.Length)
                    return Fail(result, index, "Invalid exploration anchor.");
                TraversalNode a = world.Route.Nodes[link.FromNode], b = world.Route.Nodes[link.ToNode];
                if (link.Action == TraversalAction.Walk)
                { left += Math.Max(0, a.FloorX - b.FloorX); right += Math.Max(0, b.FloorX - a.FloorX); }
                up += Math.Max(0, b.FloorY - a.FloorY); down += Math.Max(0, a.FloorY - b.FloorY);
            }
            // Actual room-scale reversals, not a short jump run-up masquerading as a turn.
            if (world.Route.Profile != WorldProfile.Mixed || left < 10 || right < 10 || up < 14 || down < 14)
                return Fail(result, -1, "Exploration needs substantial horizontal and vertical direction changes.");
        }
        var resource = new TraversalStamina(settings);
        var snapshots = new Dictionary<int, List<int>>();
        int node = 0;
        var history = new List<int>();
        foreach (int index in world.Route.PrimaryLinks)
        {
            if (index < 0 || index >= world.Route.Links.Length) return Fail(result, index, "Invalid link index.");
            TraversalLink link = world.Route.Links[index];
            if (!link.Primary || link.FromNode != node) return Fail(result, index, "Disconnected directed primary route.");
            string error = CheckLink(world, link, caps, settings, resource, ref result.TotalSeconds);
            if (error != null) return Fail(result, index, error);
            history.Add(index); node = link.ToNode;
            snapshots[node] = new List<int>(history);
        }
        TraversalNode goal = world.Route.Nodes[node];
        if (goal.FloorX != world.GoalX || goal.FloorY + 1 != world.GoalY) return Fail(result, -1, "Primary route does not terminate at Goal.");
        TraversalNode spawn = world.Route.Nodes[0];
        if (world.Route.Profile == WorldProfile.Ascent && goal.FloorY <= spawn.FloorY) return Fail(result, -1, "Ascent Goal is not above Spawn.");
        if (world.Route.Profile == WorldProfile.Descent && goal.FloorY >= spawn.FloorY) return Fail(result, -1, "Descent Goal is not below Spawn.");
        // Optional branches consume the resource state reached on the actual primary route.
        var branches = new Dictionary<int, List<TraversalLink>>();
        foreach (TraversalLink link in world.Route.Links)
            if (!link.Primary)
            {
                if (!branches.ContainsKey(link.BranchId)) branches[link.BranchId] = new List<TraversalLink>();
                branches[link.BranchId].Add(link);
            }
        foreach (List<TraversalLink> branch in branches.Values)
        {
            var branchResource = new TraversalStamina(settings); double time = 0;
            int start = branch[0].FromNode;
            if (snapshots.ContainsKey(start))
                foreach (int prior in snapshots[start])
                {
                    string error = CheckLink(world, world.Route.Links[prior], caps, settings, branchResource, ref time);
                    if (error != null) return Fail(result, prior, error);
                }
            int at = start;
            foreach (TraversalLink link in branch)
            {
                if (link.FromNode != at) return Fail(result, -1, "Disconnected optional branch.");
                string error = CheckLink(world, link, caps, settings, branchResource, ref time);
                if (error != null) return Fail(result, -1, "Optional branch: " + error);
                at = link.ToNode;
            }
            // A dead end must include a directed way back; a shortcut must reconnect.
            if (at != start && !snapshots.ContainsKey(at)) return Fail(result, -1, "Optional branch has no validated return or reconnection.");
        }
        int required = requiredFragments < 0 ? world.RequiredFragments : requiredFragments;
        if (required < 1 || required > 12 || world.RequiredFragments != required || world.FragmentNodes == null || world.FragmentNodes.Length != required ||
            world.CollectionLinks == null || world.CollectionLinks.Length < 1 || world.CollectionLinks.Length > world.Route.Links.Length * 2)
            return Fail(result, -1, "Missing or unbounded diary collection itinerary.");
        var pageAt = new Dictionary<int, int>();
        int[] exploration = DiaryPlanner.ExplorationLinks(world.Route);
        if (exploration.Length < 4 || world.Route.Links[exploration[0]].FromNode != world.Route.Links[exploration[exploration.Length - 1]].ToNode)
            return Fail(result, -1, "Missing required exploration return.");
        int deadEnd = world.ExplorationNode;
        bool terminalOnBranch = false;
        foreach (int index in exploration) if (world.Route.Links[index].ToNode == deadEnd) terminalOnBranch = true;
        if (!terminalOnBranch || deadEnd == world.Route.Links[exploration[0]].FromNode)
            return Fail(result, -1, "Exploration terminal is not inside its directed branch.");
        var terminalNeighbors = new HashSet<int>();
        foreach (int index in exploration)
        {
            TraversalLink link = world.Route.Links[index];
            if (link.FromNode == deadEnd && link.ToNode != deadEnd) terminalNeighbors.Add(link.ToNode);
            if (link.ToNode == deadEnd && link.FromNode != deadEnd) terminalNeighbors.Add(link.FromNode);
        }
        if (terminalNeighbors.Count != 1) return Fail(result, -1, "Exploration objective is not a dead-end terminal.");
        var mainNodes = new HashSet<int>();
        foreach (int index in world.Route.PrimaryLinks) mainNodes.Add(world.Route.Links[index].ToNode);
        var positions = new HashSet<long> { DiaryPlanner.PositionKey(spawn), DiaryPlanner.PositionKey(goal) };
        for (int id = 0; id < required; id++)
        {
            int pageNode = world.FragmentNodes[id];
            if (pageNode < 0 || pageNode >= world.Route.Nodes.Length || pageAt.ContainsKey(pageNode)) return Fail(result, -1, "Invalid or duplicated diary node.");
            if (pageNode != deadEnd && !mainNodes.Contains(pageNode)) return Fail(result, -1, "Fragments must use the dead end and main-route anchors.");
            TraversalNode anchor = world.Route.Nodes[pageNode];
            if (!positions.Add(DiaryPlanner.PositionKey(anchor)) || !Support(world, anchor) ||
                Occupancy(world, anchor.FloorX * world.CellSize, TraversalGeometry.RootY(world, anchor, caps), caps.SoleLocalY, caps.StandingHeight) != null)
                return Fail(result, -1, "Diary objective lacks a distinct safe standing anchor.");
            pageAt.Add(pageNode, id);
        }
        if (!pageAt.ContainsKey(deadEnd)) return Fail(result, -1, "Required dead-end fragment is missing.");
        // This is a new complete route from Spawn, not a refill between its individual branches.
        var collectionResource = new TraversalStamina(settings);
        var collected = new HashSet<int>();
        node = 0; result.TotalSeconds = 0;
        foreach (int index in world.CollectionLinks)
        {
            if (index < 0 || index >= world.Route.Links.Length) return Fail(result, index, "Invalid collection link.");
            TraversalLink link = world.Route.Links[index];
            if (link.FromNode != node) return Fail(result, index, "Disconnected complete collection route or missing branch return.");
            string error = CheckLink(world, link, caps, settings, collectionResource, ref result.TotalSeconds);
            if (error != null) return Fail(result, index, "Collection route: " + error);
            node = link.ToNode;
            if (pageAt.ContainsKey(node)) collected.Add(pageAt[node]);
        }
        result.FragmentsReached = collected.Count;
        if (node < 0 || node >= world.Route.Nodes.Length || world.Route.Nodes[node].FloorX != world.GoalX ||
            world.Route.Nodes[node].FloorY + 1 != world.GoalY || collected.Count != required)
            return Fail(result, -1, "Collection route must reach every required fragment and then Goal.");
        result.Valid = true; result.Reason = "Complete collection, directed geometry and continuous stamina model accepted; physical replay still required.";
        result.RemainingStamina = collectionResource.Current; return result;
    }

    static TraversalValidation Fail(TraversalValidation result, int link, string reason)
    { result.Valid = false; result.FailedLink = link; result.Reason = reason; return result; }

    static string CheckLink(WorldData w, TraversalLink link, TraversalCapabilities c, StaminaSettings s, TraversalStamina resource, ref double totalTime)
    {
        if (link.FromNode < 0 || link.FromNode >= w.Route.Nodes.Length || link.ToNode < 0 || link.ToNode >= w.Route.Nodes.Length ||
            !Positive(link.Duration) || link.Duration > 120 || link.Path == null || link.Path.Length < 2 || !Enum.IsDefined(typeof(TraversalAction), link.Action)) return "Malformed traversal segment.";
        TraversalNode a = w.Route.Nodes[link.FromNode], b = w.Route.Nodes[link.ToNode];
        if (Math.Abs(link.Duration - link.Path[link.Path.Length - 1].Time) > .0001) return "Action duration differs from its directed path.";
        if (Math.Abs(link.Path[0].X - a.FloorX * w.CellSize) > .001 || Math.Abs(link.Path[0].Y - TraversalGeometry.RootY(w, a, c)) > .001 ||
            Math.Abs(link.Path[link.Path.Length - 1].X - b.FloorX * w.CellSize) > .001 || Math.Abs(link.Path[link.Path.Length - 1].Y - TraversalGeometry.RootY(w, b, c)) > .001) return "Path does not connect its stated floor anchors.";
        if (!Support(w, a) || !Support(w, b)) return "Missing landing/support floor.";
        if (link.Action == TraversalAction.Rest && (!a.SafeRest || link.FromNode != link.ToNode)) return "Recovery requires a safe stationary floor.";
        if ((link.Action == TraversalAction.Walk || link.Action == TraversalAction.Sprint || link.Action == TraversalAction.Crawl) && a.FloorY != b.FloorY) return "Ground locomotion cannot bridge a height discontinuity.";
        if (link.Action == TraversalAction.Climb && !HasClimb(w, link.ClimbX, a, b)) return "No designated climb covers the segment.";
        double height = link.Action == TraversalAction.Crawl ? c.CrouchedHeight : c.StandingHeight;
        bool groundMotion = link.Action == TraversalAction.Walk || link.Action == TraversalAction.Sprint || link.Action == TraversalAction.Crawl || link.Action == TraversalAction.Rest;
        if (groundMotion)
        {
            double root = TraversalGeometry.RootY(w, a, c);
            foreach (TrajectorySample point in link.Path)
                if (Math.Abs(point.Y - root) > .001 || (link.Action == TraversalAction.Rest && Math.Abs(point.X - a.FloorX * w.CellSize) > .001)) return "Ground action contains an airborne or moving-rest trajectory.";
            double speed = link.Action == TraversalAction.Crawl ? c.CrawlSpeed : link.Action == TraversalAction.Sprint ? c.SprintSpeed : c.WalkSpeed;
            for (int i = 1; i < link.Path.Length; i++)
                if (link.Path[i].Time - link.Path[i - 1].Time + .0001 < Math.Abs(link.Path[i].X - link.Path[i - 1].X) / speed * 1.1) return "Ground action exceeds measured speed with safety margin.";
            int leftCell = Math.Min(a.FloorX, b.FloorX), rightCell = Math.Max(a.FloorX, b.FloorX);
            for (int x = leftCell; x <= rightCell; x++) if (!Solid(w, x, a.FloorY)) return "Ground action has a gap in its support floor.";
        }
        if (link.Action == TraversalAction.Climb)
        {
            bool up = b.FloorY > a.FloorY;
            if (link.Path.Length != (up ? 5 : 4)) return "Climb has no explicit dismount phases.";
            double startY = TraversalGeometry.RootY(w, a, c), endY = TraversalGeometry.RootY(w, b, c);
            if (Math.Abs(link.Path[1].X - link.ClimbX) > .001 || Math.Abs(link.Path[1].Y - startY) > .001 || Math.Abs(link.Path[2].X - link.ClimbX) > .001 || Math.Abs(link.Path[2].Y - (up ? endY - .8 : endY)) > .001) return "Climb leaves its designated shaft or capture height.";
            if (link.Path[1].Time + .0001 < Math.Abs(link.ClimbX - a.FloorX * w.CellSize) / c.WalkSpeed * 1.1 ||
                link.Path[2].Time - link.Path[1].Time + .0001 < Math.Abs(link.Path[2].Y - startY) / c.ClimbSpeed * 1.2) return "Climb exceeds measured approach or vertical speed.";
            if (up)
            {
                if (Math.Abs(link.Path[3].X - link.ClimbX) > .001 || Math.Abs(link.Path[3].Y - endY) > .001 || Math.Abs(link.Path[4].X - link.ClimbX) > 1 ||
                    link.Duration - link.Path[2].Time + .0001 < c.PullUpSeconds * 1.15 + .04) return "Pull-up exceeds measured reach or duration.";
            }
            else if (link.Duration - link.Path[2].Time + .0001 < Math.Abs(b.FloorX * w.CellSize - link.ClimbX) / c.WalkSpeed * 1.1) return "Climb dismount exceeds measured walking speed.";
        }
        for (int i = 1; i < link.Path.Length; i++)
        {
            TrajectorySample from = link.Path[i - 1], to = link.Path[i];
            if (!Finite(to.Time) || to.Time <= from.Time || !Finite(to.X) || !Finite(to.Y)) return "Invalid directed path samples.";
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) / .025));
            if (steps > 10000) return "Unbounded path sweep.";
            for (int step = 0; step <= steps; step++)
            {
                double t = step / (double)steps, x = from.X + (to.X - from.X) * t, y = from.Y + (to.Y - from.Y) * t;
                string obstruction = Occupancy(w, x, y, c.SoleLocalY, height);
                if (obstruction != null) return obstruction + " at (" + x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ").";
            }
        }
        if (link.Action == TraversalAction.Jump || link.Action == TraversalAction.DoubleJump)
        {
            TrajectorySample[] trace = link.Action == TraversalAction.Jump ? c.SingleJumpSamples : c.DoubleJumpSamples;
            if (link.Path.Length != trace.Length + 1) return "Jump does not use the measured trace and landing correction.";
            for (int i = 0; i < trace.Length; i++)
                if (Math.Abs((link.Path[i].Y - link.Path[0].Y) - trace[i].Y) > .015 || Math.Abs((link.Path[i].X - link.Path[0].X) - Math.Sign(b.FloorX - a.FloorX) * trace[i].X) > .015 || Math.Abs(link.Path[i].Time - trace[i].Time * 1.1) > .0001)
                    return "Jump trajectory exceeds measured capabilities.";
            double correction = Math.Abs(link.Path[link.Path.Length - 1].X - link.Path[trace.Length - 1].X);
            if (link.Duration - link.Path[trace.Length - 1].Time + .0001 < correction / c.WalkSpeed * 1.15) return "Landing correction is faster than measured ground motion.";
            if (!HasRunUp(w, link, c)) return "Jump lacks its measured sprint run-up on a floor.";
            double landingX = link.Path[trace.Length - 1].X, landingY = link.Path[trace.Length - 1].Y + c.SoleLocalY;
            int cellX = (int)Math.Floor(landingX / w.CellSize + .5), cellY = (int)Math.Round(landingY / w.CellSize - .5);
            if (!Solid(w, cellX, cellY)) return "Measured jump has no physical landing before walking correction.";
        }
        bool exerting = link.Action == TraversalAction.Sprint || link.Action == TraversalAction.Jump || link.Action == TraversalAction.DoubleJump || link.Action == TraversalAction.Crawl || link.Action == TraversalAction.Climb;
        double rate = link.Action == TraversalAction.Climb ? s.ClimbDrainPerSecond : link.Action == TraversalAction.Crawl ? s.CrawlDrainPerSecond : exerting ? s.SprintDrainPerSecond : 0;
        int ticks = (int)Math.Ceiling(link.Duration / c.FixedStep);
        bool paidDiscrete = false;
        for (int tick = 0; tick < ticks; tick++)
        {
            float dt = (float)c.FixedStep;
            double elapsed = tick * c.FixedStep;
            double tickRate = rate; bool tickExerting = exerting;
            bool grounded = link.Action == TraversalAction.Rest || link.Action == TraversalAction.Walk;
            bool walking = link.Action == TraversalAction.Walk;
            if (link.Action == TraversalAction.Climb)
            {
                if (link.Path.Length != (b.FloorY > a.FloorY ? 5 : 4)) return "Climb must include approach, designated vertical travel and dismount.";
                bool approach = elapsed < link.Path[1].Time;
                bool vertical = !approach && elapsed < link.Path[2].Time;
                tickRate = vertical ? s.ClimbDrainPerSecond : 0;
                tickExerting = !approach; grounded = approach; walking = approach;
                if (!approach && !vertical && !paidDiscrete && b.FloorY > a.FloorY)
                {
                    if (!resource.TrySpend(s.LedgePullUpCost + s.LedgeHangDrainPerSecond * .04f)) return "Insufficient stamina for pull-up after the climb.";
                    paidDiscrete = true;
                }
            }
            if (link.Action == TraversalAction.DoubleJump && !paidDiscrete && elapsed >= c.DoubleJumpPressSeconds)
            {
                if (!resource.TrySpend(s.DoubleJumpCost)) return "Insufficient stamina for the second jump.";
                paidDiscrete = true;
            }
            if (tickRate > 0 && !resource.TrySpend((float)(tickRate * c.FixedStep))) return "Stamina exhausted across the continuous route.";
            resource.Recover(dt, grounded, walking, tickExerting);
        }
        totalTime += ticks * c.FixedStep; return null;
    }

    static bool Support(WorldData w, TraversalNode node) { return Solid(w, node.FloorX, node.FloorY); }
    static bool HasRunUp(WorldData w, TraversalLink jump, TraversalCapabilities caps)
    {
        foreach (TraversalLink before in w.Route.Links)
            if (before.Primary && before.ToNode == jump.FromNode && before.Action == TraversalAction.Sprint && before.Duration >= caps.JumpRunUpSeconds &&
                Math.Sign(w.Route.Nodes[before.ToNode].FloorX - w.Route.Nodes[before.FromNode].FloorX) == Math.Sign(w.Route.Nodes[jump.ToNode].FloorX - w.Route.Nodes[jump.FromNode].FloorX))
            {
                TraversalNode a = w.Route.Nodes[before.FromNode], b = w.Route.Nodes[before.ToNode];
                for (int x = Math.Min(a.FloorX, b.FloorX); x <= Math.Max(a.FloorX, b.FloorX); x++) if (!Solid(w, x, a.FloorY)) return false;
                return true;
            }
        return false;
    }
    static bool Solid(WorldData w, int x, int y) { return x >= 0 && y >= 0 && x < w.Tiles.GetLength(0) && y < w.Tiles.GetLength(1) && w.Tiles[x, y] == 1; }
    static bool HasClimb(WorldData w, double x, TraversalNode a, TraversalNode b)
    {
        if (w.ClimbRegions == null) return false;
        foreach (TileRegion r in w.ClimbRegions)
            if (x >= (r.MinX - .5) * w.CellSize && x <= (r.MaxX + .5) * w.CellSize && r.MinY <= Math.Min(a.FloorY, b.FloorY) + 1 && r.MaxY >= Math.Max(a.FloorY, b.FloorY) + 1) return true;
        return false;
    }
    static string Occupancy(WorldData w, double x, double rootY, double sole, double height)
    {
        double bottom = rootY + sole, top = bottom + height + .025, left = x - .325, right = x + .325;
        if (left < .25 || right > (w.Tiles.GetLength(0) - 1.5) * w.CellSize || bottom < .5 || top >= (w.Tiles.GetLength(1) - 1) * w.CellSize) return "Outside safe world bounds";
        int minX = Math.Max(0, (int)Math.Floor(left / w.CellSize - .5)), maxX = Math.Min(w.Tiles.GetLength(0) - 1, (int)Math.Ceiling(right / w.CellSize + .5));
        int minY = Math.Max(0, (int)Math.Floor(bottom / w.CellSize - .5)), maxY = Math.Min(w.Tiles.GetLength(1) - 1, (int)Math.Ceiling(top / w.CellSize + .5));
        for (int tx = minX; tx <= maxX; tx++) for (int ty = minY; ty <= maxY; ty++)
            if (Solid(w, tx, ty) && (tx + .5) * w.CellSize > left + .00001 && (tx - .5) * w.CellSize < right - .00001 && (ty + .5) * w.CellSize > bottom + .00001 && (ty - .5) * w.CellSize < top - .00001) return "Insufficient collider clearance";
        if (w.VoidRegions != null) foreach (TileRegion r in w.VoidRegions)
            if (x > (r.MinX - .5) * w.CellSize && x < (r.MaxX + .5) * w.CellSize && rootY > (r.MinY - .5) * w.CellSize && rootY < (r.MaxY + .5) * w.CellSize) return "Path crosses a lethal region";
        return null;
    }
}
