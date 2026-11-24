using System.Collections.Generic;

// Exercise the production C# exception boundary; PowerShell delegate exceptions are wrapped.
public static class SpatialRunProbe
{
    public static int[] Generate(TraversalCapabilities caps, int count)
    {
        int seed = -1;
        var session = new WorldRunSession(() => ++seed);
        var seeds = new List<int>();
        for (int level = 1; level <= count; level++)
        {
            WorldData world = session.GenerateNext(level, false, 0, candidate =>
                ProceduralWorld.GenerateTraversal(new TraversalGenerationSettings
                { Layout = TraversalLayout.Random, RequiredFragments = 12 }, caps, new StaminaSettings(), candidate));
            if (world.Route.Profile != WorldProfile.Mixed) throw new System.Exception("Normal run accepted a linear fallback.");
            seeds.Add(world.Seed);
        }
        return seeds.ToArray();
    }
}
