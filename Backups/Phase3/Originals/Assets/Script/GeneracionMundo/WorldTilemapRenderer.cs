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
    }

    public static Vector3 CellCenter(Tilemap tilemap, int x, int y)
    {
        return tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
    }
}
