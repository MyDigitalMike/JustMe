# JustMe Phase 3 execution

Spec: docs/design/CORE_GAMEPLAY.md plus user Phase 3 attachment.

Goal: hard traversal/exploration, measured physical capabilities, stamina-aware deterministic worlds, near-total darkness, preserved assets and transitions.

Alternatives: (1) extend connected-air acceptance — insufficient; (2) route-first chambers/links with directed action/stamina checks — selected, bounded and testable; (3) full physical search across every cell — too costly for runtime generation. Reserved route geometry constrains cellular terrain; optional branches need not reach Goal. Avoid flat fallbacks.

A. Back up touched files; implement stamina arithmetic/config and Player traversal states, collision casts and standing clearance. Keep serialization, normal-based grounding and double jump. Measure real walking/sprint/jumps, crawl clearance, climb and ledges using a focused local PhysicsScene2D fixture in Editor/Play. Persist measurements for model constraints.
B. Add explicit horizontal/ascent/descent/mixed profiles to the existing generator. Keep Perlin/cellular methods; carve a planned nonflat mandatory route, bounded voids and climbable regions, low tunnels, landings/rest points and optional branches. Local terrain scale only; no global physics settings.
C. Validate swept geometry, directed actions and stamina across whole routes, including explicit recovery time on safe floors. Reject/diagnose invalid segments, bound repair attempts, keep seed/config reproducible. Pure contract tests include valid and invalid cases. Physically replay representative route segments/world paths.
D. Tune existing local Light2D presentation, remove distant unlit background visibility, keep UI readable. Existing URP Renderer2D only; configurable radius/intensity/falloff/darkness.
E. Remove active enemy/coin/spring clutter while retaining assets. Replace offscreen teleport with explicit lethal-void handling. Safely correct MainMenu's optional uGUI references (PanelSettings is a GameObject, not a UI Toolkit PanelSettings asset). Inspect TMP resource delta and request specific approval before replacement/migration if needed.
F. Reuse independent reviewer for motor/validation/physics; fix findings, compile matching Editor, run focused tests and observe Play/scene transitions. Archive transient probes with metas outside Assets. Record measured vs model-only guarantees and limitations.

Constraints: no package changes, external art, mass moves, Git mutations, broad pipelines or save format changes. Work in live existing Editor; no alternate Editor/worktree migration. No claim that all seeds are physically completed. New public APIs are additive; preserve existing GUIDs/serialized names and old signatures where practical.

Completed 2026-11-11: A-F implemented and reviewed. Native Unity compilation exit 0; 38 movement checks, 4 compound climb-exit cases, four complete seed-1 profile routes, normal-fall/Goal/void probes and GameOver/MainMenu/New Game UI verified. Temporary probes archived with metas. Results and limitations: docs/phase3-report.md. Pure tests: stamina 161 assertions; world suite 125 assertions. Human difficulty/camera/mobile playtest remains a tuning step; no claim for physical completion of every seed.
