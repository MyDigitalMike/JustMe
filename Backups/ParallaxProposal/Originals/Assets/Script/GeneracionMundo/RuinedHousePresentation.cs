using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Presentation only. This hierarchy never contains colliders, triggers or movement scripts.
public sealed class RuinedHousePresentation : MonoBehaviour
{
    GameObject artwork;
    TilemapRenderer physicalRenderer;
    bool originalVisibility;
    const int MaximumRooms = 36;

    public void Rebuild(WorldData world, Tilemap terrain, RuinedHouseTheme theme)
    {
        Clear();
        if (world.Route == null || theme == null || !theme.Ready || !theme.applyToGeneratedWorlds) return;
        artwork = new GameObject("House artwork");
        artwork.transform.SetParent(terrain.transform, false);
        var terrainRenderer = terrain.GetComponent<TilemapRenderer>();
        physicalRenderer = terrainRenderer; originalVisibility = terrainRenderer.enabled;
        GameObject skinObject = new GameObject("House terrain skin", typeof(Tilemap), typeof(TilemapRenderer));
        skinObject.transform.SetParent(artwork.transform, false);
        Tilemap skin = skinObject.GetComponent<Tilemap>();
        skin.tileAnchor = terrain.tileAnchor; skin.orientation = terrain.orientation; skin.orientationMatrix = terrain.orientationMatrix;
        var skinRenderer = skinObject.GetComponent<TilemapRenderer>();
        skinRenderer.sharedMaterial = terrainRenderer.sharedMaterial;
        skinRenderer.sortingLayerID = terrainRenderer.sortingLayerID; skinRenderer.sortingOrder = terrainRenderer.sortingOrder;
        int columns = world.Tiles.GetLength(0), rows = world.Tiles.GetLength(1);
        var surfaceTiles = new TileBase[columns * rows];
        for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
            if(world.Tiles[x,y]==1) surfaceTiles[x+y*columns]=theme.Solid(world,x,y);
        skin.SetTilesBlock(new BoundsInt(0,0,0,columns,rows,1),surfaceTiles);
        // Keep the original RuleTile and its exact physics outlines in the physical map.
        terrainRenderer.enabled = false;
        GameObject wallObject = new GameObject("Domestic back walls", typeof(Tilemap), typeof(TilemapRenderer));
        wallObject.transform.SetParent(artwork.transform, false);
        Tilemap walls = wallObject.GetComponent<Tilemap>();
        walls.tileAnchor = terrain.tileAnchor; walls.orientation = terrain.orientation; walls.orientationMatrix = terrain.orientationMatrix;
        walls.color = new Color(.57f, .61f, .65f, 1);
        TilemapRenderer wallRenderer = wallObject.GetComponent<TilemapRenderer>();
        wallRenderer.sharedMaterial = terrainRenderer.sharedMaterial;
        wallRenderer.sortingLayerID = terrainRenderer.sortingLayerID;
        wallRenderer.sortingOrder = terrainRenderer.sortingOrder - 8;
        var used = new HashSet<int>();
        Vector3 anchor = terrain.GetCellCenterLocal(Vector3Int.zero);
        int rooms = 0;
        // Spawn is a recognizable bedroom; the rest of the house emerges selectively.
        for (int i = 0; i < world.Route.Nodes.Length && rooms < MaximumRooms; i++)
        {
            TraversalNode node = world.Route.Nodes[i];
            uint choice = RuinedHouseTheme.Hash(world.Seed, node.FloorX, node.FloorY);
            if (i != 0 && (!node.SafeRest || choice % 4 == 0)) continue;
            int x = node.FloorX, floor = node.FloorY, width = world.Tiles.GetLength(0), height = world.Tiles.GetLength(1);
            int wallHeight = i == 0 ? 6 : 5 + (int)(choice % 4);
            if (floor < 0 || floor + wallHeight >= height || x < 1 || x + 1 >= width || world.Tiles[x, floor] != 1) continue;
            int left = x, right = x;
            // Only support-backed rooms: never paint paper walls over a lethal opening.
            while (left > 1 && x - left < 5 && RoomColumn(world, left - 1, floor, wallHeight)) left--;
            while (right < width - 2 && right - x < 6 && RoomColumn(world, right + 1, floor, wallHeight)) right++;
            if (right - left < 4 || !RoomColumn(world, x, floor, wallHeight)) continue;
            bool overlaps = false;
            for (int xx = left; xx <= right; xx++) for (int yy = floor + 1; yy <= floor + wallHeight; yy++)
                if (used.Contains(xx + yy * width)) overlaps = true;
            if (overlaps) continue;
            bool peeled = i != 0 && choice % 3 == 0;
            for (int xx = left; xx <= right; xx++)
                for (int yy = floor + 1; yy <= floor + wallHeight; yy++)
                {
                    // Broken upper corners make wallpaper a room remnant, not a map boundary.
                    if (yy > floor + wallHeight - 2 && (xx == left || xx == right) && ((choice + (uint)xx) & 1) == 0) continue;
                    walls.SetTile(new Vector3Int(xx, yy, 0), theme.Wall(world.Seed + i * 17, xx, yy, peeled));
                    used.Add(xx + yy * width);
                }
            int prop = i == 0 ? 0 : rooms == 1 || choice % 5 == 0 ? 4 : choice % 2 == 0 ? 1 : 5;
            float center = (left + right) * .5f + anchor.x;
            float baseY = floor + anchor.y + .5f;
            AddProp(theme, prop, new Vector2(center, baseY), prop == 1 ? 4.8f : prop == 5 ? 1.7f : 3.2f,
                prop == 5 ? 2.2f : 0, terrainRenderer, rooms);
            // One secondary wall detail; avoid repeating the same whole room recipe.
            if (right - left >= 7)
                AddProp(theme, choice % 2 == 0 ? 2 : 3, new Vector2(left + anchor.x + 1.6f, baseY), 2.7f, 2.5f, terrainRenderer, rooms);
            if (choice % 4 == 1)
                AddProp(theme, 6, new Vector2(right + anchor.x, baseY), 2.2f, wallHeight - 2.2f, terrainRenderer, rooms);
            rooms++;
        }
    }

    static bool RoomColumn(WorldData world, int x, int floor, int height)
    {
        if (world.Tiles[x, floor] != 1) return false;
        for (int y = floor + 1; y <= floor + height; y++) if (world.Tiles[x, y] != 0) return false;
        return true;
    }

    void AddProp(RuinedHouseTheme theme, int index, Vector2 position, float height, float lift, TilemapRenderer terrain, int room)
    {
        Sprite sprite = theme.props[index];
        if (sprite == null) return;
        var prop = new GameObject("House detail " + room + " / " + sprite.name, typeof(SpriteRenderer));
        prop.transform.SetParent(artwork.transform, false);
        float scale = height / Mathf.Max(.01f, sprite.bounds.size.y);
        prop.transform.localScale = Vector3.one * scale;
        prop.transform.localPosition = new Vector3(position.x, position.y + lift - sprite.bounds.min.y * scale, 0);
        var renderer = prop.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sharedMaterial = terrain.sharedMaterial; renderer.sortingLayerID = terrain.sortingLayerID;
        renderer.sortingOrder = terrain.sortingOrder - 2;
        renderer.color = new Color(.87f, .88f, .9f, 1);
    }

    public void Clear()
    {
        if (physicalRenderer != null) physicalRenderer.enabled = originalVisibility;
        physicalRenderer = null;
        if (artwork == null) return;
        artwork.SetActive(false);
        if (Application.isPlaying) Destroy(artwork); else DestroyImmediate(artwork);
        artwork = null;
    }

    void OnDestroy() { Clear(); }
}
