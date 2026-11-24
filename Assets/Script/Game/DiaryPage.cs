using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class DiaryPage : MonoBehaviour
{
    Rigidbody2D collector;
    Func<int, int, bool> collect;
    int revision;
    bool collected;
    public int Id { get; private set; }
    public int NodeIndex { get; private set; }

    public void Initialize(int id, int nodeIndex, int levelRevision, Rigidbody2D player, Func<int, int, bool> tryCollect)
    {
        Id = id; NodeIndex = nodeIndex; revision = levelRevision; collector = player; collect = tryCollect;
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || collector == null || other.attachedRigidbody != collector || collect == null) return;
        if (!collect(Id, revision)) return;
        collected = true; gameObject.SetActive(false); Destroy(gameObject);
    }

    // Runtime prototype shared by the level's pages; no imported art or lights.
    public static Sprite CreatePaperSprite()
    {
        const int width = 12, height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Diary fragment prototype"; texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool cut = x > 8 && y > 12 && x + y > 25;
            bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
            bool line = x > 2 && x < 9 && (y == 4 || y == 7 || y == 10);
            pixels[x + y * width] = cut ? Color.clear : border ? new Color(.55f, .47f, .3f) :
                line ? new Color(.55f, .51f, .4f) : new Color(.95f, .9f, .7f);
        }
        texture.SetPixels(pixels); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 16);
    }
}
