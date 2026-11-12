# Phase 2 visual choices

Confirmed locally: active GraphicsSettings references LRP, which selects Luces (Renderer2D). URP 17.6.0 is installed. No pipeline migration is necessary. The new scene-local WorldPresentation prefab supplies modest global ambient light and a warm player-local cave light; it does not change pipeline settings, existing materials or the timed collectible light.

Terrain remains SueloAvanzado, the existing custom RuleTile, palette Basica and set-cave_bright sprites: 48 PPU, point filtering, one-world-unit cells. Existing sprite contours and composite collision remain intact. The renderer now submits logical data in a batch, refreshes RuleTile neighbours, processes TilemapCollider changes and generates composite geometry before positioning actors. Decorative background sprites have no colliders.

Player Idle (1) is 416x454 at 450 PPU; its opaque bottom is local y=-0.5. The scene foot circle reaches -0.462051 and the box -0.391133. This is a mixed-resolution art set, not a verified pixel-perfect presentation. Do not change collider offsets to compensate for camera sampling. Pixel alignment needs a fixed target resolution/zoom and a coherent pixel grid before choosing a PixelPerfectCamera package or reimport settings.

The prototype reuses existing Nature cloudLayer1/2 sprites at independent horizontal factors 0.2 and 0.5, three repeats per layer, behind the terrain and above the existing nebula. Camera-relative positioning stops autonomous drift in the procedural scene. Sprite copies are created in the prefab, not allocated per frame. The source sprites are existing high-resolution art; this demonstrates depth, not the final art direction.

External options (recommendations only; nothing downloaded/imported):

| Set | Verified license/scale | Recommendation |
| --- | --- | --- |
| [Kenney Pixel Platformer](https://kenney.nl/assets/pixel-platformer) | CC0; 18x18 tiles | Strong option for a coherent pixel terrain set. Import at 18 PPU for one-unit cells, with point filtering; agree a matching character/background resolution first. Use surface tops, corners and solid interiors from one set. |
| [Kenney New Platformer Pack](https://kenney.nl/assets/new-platformer-pack) | CC0; 440 files; tile dimensions not stated on the source page | Alternative for a smoother character/environment style. Verify the archive dimensions before choosing PPU. Do not assume it matches the current 48-pixel grid. |
| [Kenney Background Elements Remastered](https://kenney.nl/assets/background-elements-remastered) | CC0; 90 files | Suitable for layered silhouettes/biomes after choosing the terrain style. Scale as background art, independent from collision. |

[Official Tilemap Extras](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@10.0/manual/index.html) provides RuleTile and AnimatedTile. The project already contains a legacy custom RuleTile; importing another implementation now adds migration/name-conflict risk without a demonstrated need. RuleTile is the relevant autotiling tool; AnimatedTile is justified only for animated decorations such as water, not static terrain. Package compatibility and existing tile GUID migration must be checked before any installation, which requires approval.

Next: tune cave density and routes while playtesting, then select one art direction. A connected air graph does not prove that every shaft is climbable with the current jump. Only the continuous main gallery is guaranteed walkable by generation contracts.
