# JustMe Phase 2

Source: user Phase 2 attachment (2026-11-11); Unity 6000.6.8f1.

Goal: improve the existing platformer in verifiable stages, preserving GUIDs, legacy generators, save format, packages, and recoverable cleanup backups.

Architecture choice: a pure C# surface/cave generator plus a small Tilemap adapter, selected explicitly in Libro. Keeping all logic in Libro is smaller initially but perpetuates scene coupling; replacing every algorithm would risk legacy levels. Use the additive adapter and retain the legacy path. No new dependencies or runtime framework.

1. Cleanup: observe GameOver -> MainMenu and compile in Editor. Distinguish preexisting TMP resource-version and menu optional-reference errors from cleanup regressions.
2. Physics: isolated local PhysicsScene2D measurements of real scene colliders at flat floor/corners; compare physical support with sprite geometry. Player owns contact-normal ground state; sample hardware/touch input in Update, consume movement/jump in FixedUpdate. Keep serialized names and existing double jump. Preserve valid collider geometry unless measurement justifies a change.
3. Generation: configuration, deterministic local PRNG and Perlin surface, double-buffered Moore smoothing, region connection to a safe gallery, bounded solid/air data and safe player/book cells. Validate repeated/multiple seeds, connectivity and floor clearance. Renderer batches tiles and refreshes composite geometry before placement. Legacy methods remain available.
4. Visuals: camera-relative background layers using existing art, no collider changes or per-frame allocations. Fix only demonstrated camera timing defects. Inspect actual RuleTile/PPU and provide CC0 art recommendations without imports. Use existing URP 2D only; no material/pipeline migration.
5. Presentation: disable only the referenced enemy-counter object and remove the per-frame enemy search. Preserve enemy logic/prefabs and serialized references.
6. Review: independent physics/architecture review; address concrete findings. Editor compilation, focused isolated physics and generation checks, main Play Mode and runtime console. Keep verification artifacts under Backups/Phase2; temporary Editor probes are removed from Assets after checks.

Acceptance: no new compiler errors; single ground authority; wall contact is not grounded; jump/land/edge fall and keyboard/touch branches work; deterministic maps with connected air and safe main route; parallax stationary with stationary camera; counter hidden; scene/prefab script references intact. Connectivity is not a claim that every cave ledge is platformer-reachable.

Approval boundaries: no external imports, package changes, broad asset moves, render migration, save changes, commits or Git configuration changes.
