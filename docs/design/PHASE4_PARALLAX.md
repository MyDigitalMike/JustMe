# Future layered parallax - proposal, not implemented

Latest direction (2026-11-23): the user approved Backups/HouseRuins/approved-concept.png as the visual reference and wants to explore house/ruin backgrounds moving at different speeds. Current tile materials (stone, wood, wallpaper and peeled wallpaper/plaster) are accepted; furniture composition is provisional and should become deliberate scenery or selected mini-obstacles later. See [persistent checkpoint](../context/source/VISUAL_CHECKPOINT.md). No parallax implementation, furniture colliders or further gameplay changes are requested now.

The first house tile integration uses a very dark navy void; the old parallax component/assets remain inactive as reference. F8 still enables full visibility for temporary manual testing. The technical notes below are an earlier proposal, not approved final art settings. Before implementation, decide how the visible depth of the new concept should coexist with local darkness; preserve current gameplay and lighting until that decision.

Use a small scene-local component with far, middle and near layer data. Each layer owns sprites, independent horizontal/vertical camera factors, an origin and a tile size; for example far (0.08,0.08), middle (0.25,0.20), near (0.55,0.45), subject to art-direction testing. Compute position from camera displacement, never accumulated per-frame offsets. Orthographic camera framing and actual sprite bounds determine repetition coverage.

Update after Cinemachine and camera-follow (LateUpdate with explicit ordering). On a player/camera warp, rebase layer origins with the same warp delta to prevent snapping or slow catch-up. Do not add a second gameplay camera controller.

Use named background Sorting Layers in far-to-near order, all behind terrain/player; decide layer IDs when implementing, without silently rewriting ProjectSettings now. Keep decorative layers free of Collider2D/Rigidbody2D and gameplay triggers. Mesh depth is supplementary, never a substitute for sorting rules in Renderer2D.

Repeat in both axes for vertical and horizontal worlds. Prefer edge-matching source art and a reusable grid of tiles sized for the orthographic viewport plus one tile margin on each side. Wrap complete rows/columns using floor-based modulo, including negative coordinates, and rebase at large coordinates. Verify seams at camera motion, zoom, aspect changes and warps; do not assume three horizontal copies cover vertical shafts.

Far/middle/near artwork must obey local darkness. Prefer Sprite-Lit rendering on compatible sorting layers and the same restricted visibility region as terrain. An unlit background must use a local darkness mask that never reveals distant silhouettes, topology or unexplored space. No additional per-page lights, permanent map reveal, per-frame global searches or new objects each frame.

When actual background art is selected, prototype these layers locally and verify in the existing Renderer2D pipeline. No imports, shader/pipeline changes or large rendering framework are part of Phase4.
