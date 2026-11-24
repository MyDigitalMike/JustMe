using UnityEngine;
using UnityEngine.Tilemaps;

// Immutable artwork configuration. Tile occupancy remains owned by WorldData.
public sealed class RuinedHouseTheme : ScriptableObject
{
    public TileBase[] stone = new TileBase[0];
    public TileBase[] timber = new TileBase[0];
    public TileBase[] wallpaper = new TileBase[0];
    public TileBase[] plaster = new TileBase[0];
    public Sprite[] props = new Sprite[0];
    public bool applyToGeneratedWorlds = true;
    [Tooltip("Place the retained wallpaper and domestic props. Disabled while their presentation is redesigned.")]
    public bool showDomesticDetails = false;

    public bool Ready { get { return stone.Length == 16 && timber.Length == 16 && wallpaper.Length == 16 && plaster.Length == 16 && props.Length == 8; } }

    public TileBase Solid(WorldData world, int x, int y)
    {
        bool house = Hash(world.Seed, x / 16, y / 12) % 4 != 0;
        bool top = y + 1 >= world.Tiles.GetLength(1) || world.Tiles[x, y + 1] == 0;
        TileBase[] material = house && top ? timber : stone;
        return material[PatchIndex(world.Seed, x, y)];
    }

    public TileBase Wall(int seed, int x, int y, bool peeled)
    {
        return (peeled ? plaster : wallpaper)[PatchIndex(seed, x, y)];
    }

    // A coherent 4x4 texture patch, phase-shifted per seeded room; no per-cell noise seams.
    static int PatchIndex(int seed, int x, int y)
    {
        return (((x + (seed & 3)) & 3) + ((y + ((seed >> 2) & 3)) & 3) * 4);
    }

    public static uint Hash(int seed, int x, int y)
    {
        unchecked
        {
            uint value = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu;
            value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu;
            return value ^ (value >> 16);
        }
    }
}
