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
        var renderer = tilemap.GetComponent<TilemapRenderer>();
        environment.ConfigureArtwork(renderer.sortingLayerID, renderer.sortingOrder);
        environment.ledgeDecorations = FindLedgeDecorations(world, tilemap, player);
        var hero = player.GetComponent<HeroVisualAlignment>();
        if (hero != null) hero.ConfigureTerrainSorting(renderer.sortingLayerID, renderer.sortingOrder);
        environment.DrawDesignatedSurfaces(renderer.sharedMaterial, cues);
        player.ConfigureTraversal(environment);
    }

    // Grid occupancy supplies candidates; actual collider hits own each final corner/normal.
    public static TraversalEnvironment.LedgeDecoration[] FindLedgeDecorations(WorldData world, Tilemap tilemap, Player player)
    {
        var result = new System.Collections.Generic.List<TraversalEnvironment.LedgeDecoration>();
        var composite = tilemap.GetComponent<CompositeCollider2D>();
        if (composite == null) return result.ToArray();
        var seen = new System.Collections.Generic.HashSet<Vector2Int>();
        var physics = tilemap.gameObject.scene.GetPhysicsScene2D();
        var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(player.collisionLayer);
        var hits = new RaycastHit2D[8];
        float extent = player.GetComponent<BoxCollider2D>().bounds.extents.x;
        int width = world.Tiles.GetLength(0), height = world.Tiles.GetLength(1);
        Vector3 stepX = tilemap.CellToWorld(Vector3Int.right) - tilemap.CellToWorld(Vector3Int.zero);
        Vector3 stepY = tilemap.CellToWorld(Vector3Int.up) - tilemap.CellToWorld(Vector3Int.zero);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            if (world.Tiles[x, y] != 1 || y + 1 < height && world.Tiles[x, y + 1] == 1) continue;
            for (int inward = -1; inward <= 1; inward += 2)
            {
                int outside = x - inward;
                if (outside >= 0 && outside < width && world.Tiles[outside, y] == 1) continue;
                Vector2 corner = CellCenter(tilemap, x, y) - stepX * (inward * .5f) + stepY * .5f;
                int count = physics.Raycast(corner + new Vector2(inward * .025f, .05f), Vector2.down, .1f, filter, hits);
                if (count == 0 || hits[0].collider != composite || hits[0].normal.y < .9f) continue;
                float top = hits[0].point.y;
                count = physics.Raycast(corner + new Vector2(-inward * .05f, -.025f), Vector2.right * inward, .1f, filter, hits);
                if (count == 0 || hits[0].collider != composite || Mathf.Abs(hits[0].normal.x) < .8f) continue;
                corner = new Vector2(hits[0].point.x, top);
                if (!player.CanStandAt(new Vector2(corner.x + inward * (extent + .06f), corner.y - player.SoleOffset + .025f), true)) continue;
                var key = new Vector2Int(Mathf.RoundToInt(corner.x * 1000), Mathf.RoundToInt(corner.y * 1000));
                if (!seen.Add(key)) continue;
                int next = x + inward;
                bool longerTop = next >= 0 && next < width && world.Tiles[next, y] == 1 && (y + 1 >= height || world.Tiles[next, y + 1] == 0);
                result.Add(new TraversalEnvironment.LedgeDecoration { Corner = corner, Inward = inward, Width = Mathf.Min(.34f, stepX.magnitude * (longerTop ? 1 : .5f)) });
            }
        }
        return result.ToArray();
    }
}
