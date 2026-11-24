using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class WorldTilemapRenderer
{
    public static void Apply(WorldData world, Tilemap tilemap, TileBase solidTile)
    {
        if (world == null || tilemap == null || solidTile == null)
            throw new ArgumentNullException("World, tilemap and solid tile are required.");
        int width = world.Tiles.GetLength(0), height = world.Tiles.GetLength(1);
        if (world.Route != null) tilemap.transform.localScale = Vector3.one * (float)world.CellSize;
        var tiles = new TileBase[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (world.Tiles[x, y] == 1) tiles[x + y * width] = solidTile;
        tilemap.ClearAllTiles();
        tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, width, height, 1), tiles);
        tilemap.RefreshAllTiles();
        var collider = tilemap.GetComponent<TilemapCollider2D>();
        if (collider != null) collider.ProcessTilemapChanges();
        var composite = tilemap.GetComponent<CompositeCollider2D>();
        if (composite != null) composite.GenerateGeometry();
        Physics2D.SyncTransforms();
        RuinedHouseTheme theme = Resources.Load<RuinedHouseTheme>("RuinedHouseTheme");
        bool useHouse = world.Route != null && theme != null && theme.Ready && theme.applyToGeneratedWorlds;
        RuinedHousePresentation house = tilemap.GetComponent<RuinedHousePresentation>();
        if (useHouse)
        {
            if (house == null) house = tilemap.gameObject.AddComponent<RuinedHousePresentation>();
            house.Rebuild(world, tilemap, theme);
        }
        else if (house != null) house.Clear();
    }

    public static Vector3 CellCenter(Tilemap tilemap, int x, int y)
    {
        return tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
    }

    public static Vector3 StandingPosition(WorldData world, Tilemap tilemap, TraversalNode node, float soleOffset)
    {
        Vector3 center = CellCenter(tilemap, node.FloorX, node.FloorY);
        Vector3 step = tilemap.CellToWorld(Vector3Int.up) - tilemap.CellToWorld(Vector3Int.zero);
        return center + step * .5f - Vector3.up * soleOffset + Vector3.up * .02f;
    }

    public static Vector3 ModelPosition(Tilemap tilemap, double x, double y)
    {
        return CellCenter(tilemap, 0, 0) + new Vector3((float)x, (float)y, 0);
    }

    public static void ConfigureTraversal(WorldData world, Tilemap tilemap, TraversalEnvironment environment, Player player)
    {
        var climbs = new System.Collections.Generic.List<Bounds>();
        foreach (TraversalLink link in world.Route.Links)
        {
            if (link.Action != TraversalAction.Climb) continue;
            TraversalNode a = world.Route.Nodes[link.FromNode], b = world.Route.Nodes[link.ToNode];
            Vector3 low = StandingPosition(world, tilemap, a.FloorY < b.FloorY ? a : b, player.SoleOffset);
            Vector3 high = StandingPosition(world, tilemap, a.FloorY > b.FloorY ? a : b, player.SoleOffset);
            Vector3 center = ModelPosition(tilemap, link.ClimbX, 0);
            center.y = (low.y + high.y) * .5f; center.z = 0;
            climbs.Add(new Bounds(center, new Vector3(.35f, high.y - low.y + .3f, 2)));
        }
        var voids = new Bounds[world.VoidRegions.Length];
        var cues = new Vector3[world.VoidRegions.Length];
        for (int i = 0; i < voids.Length; i++)
        {
            TileRegion r = world.VoidRegions[i];
            Vector3 center = ModelPosition(tilemap, (r.MinX + r.MaxX) * world.CellSize * .5, (r.MinY + r.MaxY) * world.CellSize * .5);
            voids[i] = new Bounds(center, new Vector3((float)((r.MaxX - r.MinX + 1) * world.CellSize), (float)((r.MaxY - r.MinY + 1) * world.CellSize), 2));
            cues[i] = ModelPosition(tilemap, r.CueX * world.CellSize + .15, (r.CueY - .5) * world.CellSize + .025);
        }
        environment.climbableAreas = climbs.ToArray(); environment.voidAreas = voids;
        environment.bottomVoidEnabled = true; environment.bottomVoidY = ModelPosition(tilemap, 0, -1).y;
        environment.DrawDesignatedSurfaces(tilemap.GetComponent<TilemapRenderer>().sharedMaterial, cues);
        player.ConfigureTraversal(environment);
    }
}
