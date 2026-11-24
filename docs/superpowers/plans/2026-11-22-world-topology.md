# World topology and passage clearance implementation plan

Goal: long left/right, tall ascent/descent and U, switchback S and four-arm crossing exploration, with generous ordinary headroom and deliberate low tunnels.
Architecture: pure bounded macro-route grammar feeding the existing traversal rasterizer, complete diary/stamina validator and Unity tilemap. Keep the legacy profile builder available for regressions. No movement tuning, packages, pipeline, scene or save format changes.
User authorization: direct voice request to implement these changes; execute locally in the existing project. No additional design/architecture approval inferred for reversible source changes. No Git writes.
Alternatives: changing noise alone preserves the old topology; a new unconstrained cave algorithm loses physical guarantees. Use a route graph composed of calibrated walking/jumping/climbing segments, with dynamic storage bounds and mirrored directions. A rectangular backing array does not define the playable outline.

- [x] Add failing pure tests: explicit layouts, left/right jump symmetry, >=60u vertical extent, direction reversals, deterministic random layout choice, three/twelve distinct pages, full return/stamina acceptance, ordinary floor-to-ceiling clearance >=2.5u and crawl-specific restriction.
- [x] Add TraversalLayout, bounded seeded macro planner with existing action/link types, dynamic bounds <=384 cells per axis, random runtime layout and preserved debug reproduction.
- [x] Reuse rasterization and generalize signed jump traces, supports, crawl roofs and localized voids. Ordinary passage height 2.5u; preserve actual crawl obstacles and reachable climb wall side. Reject invalid candidates with existing bounded validation.
- [x] Run focused pure cases and existing regressions once, review collision/continuous stamina implications. Native replay all new layouts in both directions and representative twelve-page routes with the actual Player/Tilemap/composite. Validate scene integration and final native compilation/Play. Archive temporary probes and update checkpoint at completion.

Review focus: mirrored landing correction; voids destroying lower routes; junction floor collisions; random default serialized settings; pages/full returns at long layouts; no accidental physics or lighting tuning.
