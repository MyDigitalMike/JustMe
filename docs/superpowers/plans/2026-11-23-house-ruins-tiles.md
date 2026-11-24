# House ruins tiles implementation plan

**Goal:** Integrate the user-approved hand-painted house-in-ruins concept as usable procedural terrain art, with domestic details and open void, before the later character redesign.

**Architecture:** Keep the existing physical Tilemap, original RuleTile physics outlines and generator exactly intact. A native ScriptableObject palette supplies a collider-free visual Tilemap over the same occupied cells, hiding only the original renderer. A separate renderer builds collider-free room wallpaper and domestic props behind the player. Reuse the installed Sprite Editor provider and matching Editor; no RuleTile/extras, packages, Pipeline or CLI installation. Original textures, tile GUIDs, scenes and prefabs remain recoverable and unchanged.

**Alternatives:** Replacing only the stone texture is small but does not convey a house; rebuilding topology as physical rooms would disturb validated routes. Selected: an additive visual skin with coherent stone/wood/plaster and selectively furnished rooms. User approved the visual concept and requested direct tile integration, prioritizing it over character art.

**Tech:** Unity 6000.6.8f1, installed 2D Sprite/Tilemap and URP 17.6; built-in ImageGen for original artwork. No Git actions; verified backups in Backups/HouseRuins.

- [x] Generate and inspect an opaque material atlas and a transparent domestic-prop atlas from the approved image; save source prompts and project copies.
- [x] Import/slice through Sprite Editor Data Provider capability checks; preserve all pre-existing .meta IDs. Create native tiles and palette asset.
- [x] Integrate palette selection and collider-free decoration at WorldTilemapRenderer.Apply, preserving exactly the original RuleTile occupancy, collision outlines, scale and gameplay data. Use stable seed-based room placement and retire old decoration immediately on rebuild. Match void color in WorldLighting while preserving F8 behavior.
- [x] Native regression: compare every original/current tile and CompositeCollider2D path point; assert no decorative colliders, unchanged world signature/player/route/diary data, matching skin world coordinates, deterministic material/room placement and cleanup on regeneration. Added a failing/passing furniture support regression for the real zero-anchor map.
- [x] Compile in Unity and inspect actual Spawn presentation, F8 and direct generation of level 2 with its fragment count. Capture actual gameplay, observe zero console errors, retire temporary Editor instrumentation after Play is stopped; record final changed files/results. Full collection/Goal replay was not repeated because its source and exact collision geometry are unchanged; no such new gameplay result is claimed.

**Review focus:** visuals must not suggest extra physical platforms; furniture is background decoration; wallpaper must not cover voids or every passage; floor boundaries and warnings remain readable; sprite transparency and atlas seams need visual inspection. This is a first modular art integration, not a promise that procedural geometry equals the illustrated composition exactly.

Ruling: the existing SueloAvanzado is a project-local RuleTile with Sprite collider outlines. Replacing it with a Grid collider tile could change collision edges. Keep that original tile data in the physical map and add a collider-free skin instead; compare native composite paths against the original application loop.
