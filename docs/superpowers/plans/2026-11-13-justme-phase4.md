# JustMe Phase 4 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for implementation; the independent reviewer checks significant traversal changes and the final result. No commits or worktrees are authorized for this live Editor task.

**Goal:** Quiet keyboard-first gameplay, recoverably retired obsolete prefabs, fresh validated worlds and transient diary-fragment collection before each exit.

**Architecture:** Extend the existing route-first planner with one explicit complete collection itinerary. Reuse its directed links and one continuous stamina simulation, including a return from the existing dead-end branch. Small pure classes own diary arithmetic and per-level generation; Libro remains the scene/progression coordinator. Existing Player physics and WorldLighting stay intact.

**Tech Stack:** Unity 6000.6.8f1, URP 17.6.0 Renderer2D, Rigidbody2D, uGUI/TMP, existing Cinemachine and input implementation. No dependencies added.

**Spec:** User Phase 4 attachment f97fce4a-8166-45f4-ad96-047f00cd42be, plus explicit decisions: continuous levels; one fragment on a returning branch and the remainder on the main route; 3 initially, +1 each level, maximum 12. Existing checkpoints: docs/phase3-report.md and docs/hover-fix-report.md.

## Constraints and selected approach

- Preserve Player, stamina calibration, 1-unit mandatory gaps, GUIDs, save-file fields, lighting and scene navigation.
- Three approaches considered: independent Spawn-to-Page checks are insufficient; general search over collected subsets and stamina adds unnecessary complexity; a constrained ordered collection route is selected using the user's approved branch/main-route split.
- Required fragment count is configurable but bounded to 1..12 for this prototype; default progression is 3/4/.../12. Locations are distinct validated standing anchors, excluding Spawn/Goal.
- A level's seed stays stable; new games and level transitions request fresh seeds. Explicit fixed-seed debugging remains deterministic. Compare geometry signatures with bounded retries; never return an invalid fallback. Do not persist seeds/pages into BinaryFormatter data.
- UI remains existing uGUI. Standard gameplay shows Pause only; existing text is hidden or reused for explicitly enabled Editor/development diagnostics and brief collection/locked-exit feedback. No permanent page HUD or light rewards.
- Preserve original audio/art/shared dependencies. Disable gameplay music and active parallax only. Document future layered parallax without implementing a framework.

## Review focus

- Whole collection route must visit every required objective and return from the mandatory branch with one stamina history.
- Duplicate collider callbacks, stale page callbacks and simultaneous exit events must not count twice or advance twice.
- New runs, repeated generation failure, seed replay and consecutive geometry deduplication must have explicit bounded behavior.
- Asset retirement must remove known prefab references before deletion, preserve a verified backup/metas and leave no missing prefab/script references.
- Normal HUD must remain Pause-only; diagnostic gating must work in release builds and music suppression must preserve effects/navigation.

## 1. Retire obsolete presentation and prefabs

- [x] Record native baseline, exact prefab references/dependencies, visible UI and music source.
- [x] Back up and hash the affected scripts, three prefabs/metas, Libro prefab, WorldPresentation and the two referencing scenes.
- [x] Use a temporary Editor helper to clear Libro's obsolete prefab references, remove their obsolete scene instances and retire only Assets/Prefabs/Character/JumpHard.prefab, Assets/Prefabs/UI/Coin.prefab and Assets/Prefabs/Character/EnemigoSimple.prefab after reference checks. Retain all shared art/animation/scripts.
- [x] Disable current cloud/parallax presentation, gameplay music, touch/progression HUD buttons and obsolete labels through focused Editor edits. Preserve Pause/navigation/input implementation and WorldLighting.
- [x] Remove confirmed obsolete spawn methods/fields from Libro; preserve its asset and active references. Verify affected scenes/prefabs contain no missing scripts/prefabs and none of the retired GUIDs remain referenced.

## 2. Fresh world generation

- [x] Write failing pure checks for fresh seeds, stable current seed, fixed replay, signature deduplication and exhausted bounded retries.
- [x] Add WorldRunSession with injected seed factory for tests, fixed-debug mode and bounded geometry-signature comparison. Commit state only after a world is accepted.
- [x] Change Libro default fixed mode to false and use the session for every accepted level. Continuous progression replaces the three-level cap; preserve Credits access from MainMenu and the unchanged legacy save DTO.
- [x] Verify two independent New Game flows yield different seeds and geometry signatures.

## 3. Diary collection and locked exit

- [x] Add pure DiaryProgressionSettings.RequiredForLevel(int) and DiaryProgress count/duplicate/reset checks first, including level growth, cap, invalid IDs and stale revision rejection.
- [x] Create runtime DiaryPage triggers and minimal reused/procedural paper visuals without lights or external art. Page callbacks include level revision; deactivate collected/stale pages before destroying them.
- [x] Libro resets only diary state on generation; exit remains locked until all required fragments are collected. Preserve stamina, warp cleanup, Goal frame guard and lethal-void event.
- [x] Reuse existing text for short feedback and a default-off Editor/development diagnostic toggle. No new standard HUD.

## 4. Complete collection reachability

- [x] Add required page count, distinct page nodes and explicit collection-link sequence to world data/plan without replacing existing links.
- [x] DiaryPlanner selects one dead-end objective and distributes remaining objectives over valid primary anchors. Collection itinerary traverses the whole directed branch return before the original route/rests.
- [x] Extend TraversalValidator to validate all objective anchors and the full connected collection itinerary with one stamina instance, bounded link count and final Goal. Reject missing/unreachable/duplicated objectives, disconnected returns, insufficient accumulated stamina and early termination.
- [x] Extend pure suites across all four profiles, multiple seeds and counts 3/4/12; preserve existing invalid-world cases and deterministic behavior.
- [x] Independent reviewer checks traversal/seed/progression changes; fix concrete findings before final native verification.

## 5. Unity and final checkpoint

- [x] Compile in the already installed matching Editor; do not use blocked Unity CLI or install Pipeline.
- [x] Run focused native movement regression and representative complete branch+page+Goal routes with real Player/Tilemap/composite physics.
- [x] Verify locked exit, all fragments, one advance, new seed/reset, void GameOver, Pause-only HUD, disabled music/parallax, preserved effects and default-off/explicit-on diagnostics.
- [x] Retire temporary Editor helpers with metas outside Assets; final compile/Play without instrumentation. Distinguish model acceptance from sampled physical routes.
- [x] Document lightweight future far/middle/near parallax with independent XY factors, camera warp handling, sorting, two-axis repetition, no colliders and darkness masking.
- [x] Update one concise current project checkpoint at the end, preserving historical Phase 3/hover reports. Report actual results in Spanish.
