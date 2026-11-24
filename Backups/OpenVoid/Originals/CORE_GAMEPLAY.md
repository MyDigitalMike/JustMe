# JustMe - core gameplay



Current verified checkpoint: spatial chamber and route variety, 2026-11-23 (Unity 6000.6.8f1). Phase 4 diary progression and Phase 3 movement/hover-fix calibration remain in effect.



- Hard difficulty from the beginning; no automatic easy-mode scaling.

- Atmospheric procedural exploration with almost total darkness: approximately one or two character steps of local visibility, readable silhouette and UI. No persistent illumination of unexplored terrain.

- Walk, sprint, jump, double jump, crouch/crawl, grab/release/pull up ledges, and climb only designated surfaces. No unrestricted wall climbing, wall jump or dash.

- Shared stamina: sprinting, climbing, hanging and crawling drain continuously; double jump and pull-up have discrete costs. Recovery only at grounded rest/walking after exertion delay. Walking remains available at exhaustion; climbing/hanging exhaustion drops the player. Transitions do not refill stamina.

- Explicit debug layouts retain: long horizontal runs in either direction, tall ascent/descent, U, S switchbacks and a four-arm cross with returning excursions. Ascent/descent profiles retain at least 60 world units of vertical route extent. Orthogonal traversal is composed from calibrated actions; rectangular backing storage no longer limits the playable route to a horizontal corridor. Dynamic storage is bounded to 512 cells per axis to accommodate wider chambers; Legacy profile generation remains available for regression/debugging. Goal may lie above, below or across from Spawn.

- Ordinary passages reserve at least 2.5 units of floor-to-ceiling height; the sampled native initial corridor measured about 2.99 units, versus 1.0 in the old generator for the 0.784-unit standing collider. Low crawl obstructions remain intentional local challenges. New climb landings preserve actual upper ledges without refilling their entire supporting column through crossing corridors. Turns jump across shaft openings rather than flooring over them. Only marked lethal gaps kill; return-shaft openings can remain nonlethal.

- Each accepted world needs a feasible complete collection route under measured movement, geometry and stamina constraints: visit the mandatory dead end, return, collect the remaining fragments and reach Libro with one continuous stamina history. Air connectivity or separate Spawn-to-page proofs are insufficient. Difficult mandatory segments coexist with optional reconnecting paths, backtrackable dead ends, risky shortcuts and explicit lethal hazards.

- Lethal voids lead to GameOver. Ordinary platform falls are not automatically lethal. Danger must be visible before an unavoidable commitment despite the short sight radius.

- No active enemies, coins, springs, combat, inventory or monetization. The three approved legacy prefabs are retired with original files/metas in `Backups/Phase4/before.zip`; shared art, animation, sound and Libro remain. Narrative systems are postponed.

- Continuous levels require three diary fragments initially, one more per level, capped at twelve. One fragment is at the returning dead end; the rest occupy distinct main-route anchors. Libro stays locked until all are collected. New worlds reset fragment progress without refilling stamina. Pages are triggers with paper prototype visuals and no lights.

- Each new run/level receives a fresh seed. Seeds stay stable within the level; fixed debug mode remains reproducible. Bounded retries reject consecutive identical geometry and invalid worlds. Legacy saving retains its existing format: seeds and fragments are not persisted; loading regenerates the saved level.



Prototype stamina configuration: max100; sprint20/s, climb30/s, hang14/s, crawl8/s; double jump18, pull-up15; idle recovery13/s, walking recovery5/s; recovery delay1.5s. These are tuning data, not automatic difficulty scaling.



Implementation decisions: retain Player GUID and one contact-normal ground authority; a small explicit traversal state plus independent stamina arithmetic; keep generation data and traversal validation pure C#. Plan mandatory routes before natural terrain, preserving reserved clearance and landings. Explicit rest actions model elapsed recovery rather than magically resetting stamina. Use half-unit terrain cells locally so the existing ~0.78-unit standing collider and ~0.39-unit crouched collider support real low tunnels. Preserve the legacy generator path and save format.



Keyboard prototype: A/D or arrows walk; Shift sprint; Ctrl/C crouch/crawl; Space/W jump; Up/Down on designated climbs; jump/Up pulls from a ledge; Down releases it. Existing virtual axes/buttons remain supported. Exact capabilities and conservative generation limits come from the matching Editor tests, not these design intentions.



Normal gameplay HUD contains only Pause. Existing touch-control code is retained with its old buttons hidden. A default-off Editor/development toggle exposes stamina/state/seed diagnostics; collection and locked-exit feedback is temporary. Gameplay music and the old parallax presentation are disabled; effects and local lighting remain. Designated ropes and red departure marks identify climbable shafts and lethal gaps locally; they do not illuminate the map. Future layered parallax is only a proposal in [PHASE4_PARALLAX.md](PHASE4_PARALLAX.md).



Verified at the earlier topology checkpoint: 14 complete physical collection routes (six layouts in both mirrored directions with three fragments, plus S/Cross with twelve) and 34 integration checks passed. The initial Cross return-corner failure was reproduced and resolved by separating the ascending return shaft from the exit gap; its three cases were rerun successfully. Actual page overlaps, locked/unlocked exit, level 3-to-4 continuity, twelve-page cap, Pause, MainMenu/GameOver navigation and lethal voids passed. Two independent New Games selected Ascent and Cross with different seeds/maps. Final compilation and Play Mode without temporary probes succeeded; Console showed zero errors. Player.cs and existing metas retain baseline hashes; its Phase 4 movement regression baseline is preserved. WorldLighting subsequently gained the temporary manual-test override described below.



Pure checks passed: 211 assertions across 42 layout/seed/page cases, plus all 355 Phase 4 assertions and 157 traversal regressions. Current evidence, original files/metas and recoverable probes are under `Backups/WorldTopology/`; historical Phase 4 evidence remains under `Backups/Phase4/`. The new planner is a bounded seeded grammar, not an infinite-world system or proof of every seed. Longer manual balance/visibility testing of the large levels remains necessary. VS/Unity still reports an unavailable UDP messaging port (56158); the known CLI restriction is unchanged. No packages, scenes, prefabs, security settings, player physics tuning or save-format changes were made.


Temporary manual-test lighting override (2026-11-23): Editor/development runs start with full terrain visibility. F8 in the focused Game view toggles it, including while paused; the same `Debug Full Visibility` checkbox is on WorldPresentation(Clone) > WorldLighting in the Inspector. Normal darkness settings remain intact and non-development builds ignore the override. The existing pause-menu dark veil is unchanged; for unobscured Scene inspection, resume the game and use the Editor pause control if desired. Native compilation, default bright view, dark toggle, bright toggle while paused and Scene visibility were observed; Console had zero errors. Only WorldLighting.cs changed; its meta/GUID, scenes, prefabs, movement and packages remain intact. Original source and verification are under `Backups/ManualLighting/`.

Tunnel variety correction (2026-11-23): horizontal blocks no longer stamp the same Jump/Crawl/DoubleJump sequence repeatedly. Challenge orders are drawn without replacement across the level, with seeded variable walking separations. New climbing steps vary their height and horizontal spacing, reject adjacent identical footprints and prefer unused footprint pairs; protected junctions retain required clearance. Jump reach, crawl length, rest/stamina rules, player physics and manual-lighting override are preserved. Anti-repeat checks passed for three seeds; 211 topology assertions across 42 worlds and all 14 complete native physical replays passed, including S/Cross with twelve fragments. This earlier obstacle-level change proved insufficient during manual inspection: long stair banks and the terrain silhouette still repeated. It is superseded by the spatial correction below. Source backup, checks and physical evidence are under `Backups/VarietyFix/`.

Spatial generation correction (2026-11-23): runtime Random now composes seeded exploration sections instead of selecting one complete template. Rooms interrupt vertical banks (maximum four consecutive climbs); regular chambers of independently sampled height coexist with the continuous irregular roof field. Room widths, challenge frequency, local counter-slopes and direction changes vary. Turns occur at new landings outside the previous height band to avoid drilling through a floor already traversed. Horizontal runs also insert quiet chambers rather than repeat a compulsory challenge recipe. The roof is independent of jump trajectories. Existing explicit debug layouts share the same chamber planner.

At least two returning exploration branches are required, with roots at intermediate rooms rather than a fixed Spawn branch. The required fragment uses a degree-one terminal. Its full outbound/return route is inserted at its actual primary root; the remaining pages and Goal share one continuous stamina history. Branch terminals are above the root chamber, and reservations include their maximum roof height plus separation. Upper world padding is 22 half-unit cells so high chambers are not clipped against the storage border. Calibrated jumps, crawl distance, player colliders, stamina rules, existing enum values and save format are preserved.

Observed verification for the spatial correction: 20 complete native physics replays in Unity 6000.6.8f1, traversing every returning exploration branch, collecting all fragments and reaching Goal. These comprise the six explicit layouts plus Random in both mirrored directions, S/Cross/Random with 12 fragments, and Random seeds 2/4/11 with 12 fragments; after the user selected the mixed style, all six default seeds 0/1/2/4/11/42 were replayed again with 12 fragments. Pure checks passed 211 assertions across 42 layout/seed/page cases, 355 Phase 4 assertions, 157 legacy traversal assertions and the spatial regressions including 32 normal run generations under the preferred mixed-world acceptance policy. The actual runtime Exploration seed 2 was inspected in Scene view; native preview and tile comparison are under Backups/SpatialVariety. Independent review closed without remaining blockers in scope. These are representative samples, not proof of every seed or subjective play feel.

Only TraversalTopology.cs, TraversalWorld.cs, ProceduralWorld.cs, DiaryPlanner.cs and TraversalValidator.cs changed at runtime, with focused test/checkpoint updates. Player.cs and WorldLighting.cs retain their verified baseline hashes. No package, scene, prefab, physics setting, serialized GUID or save-format change. A temporary hot-reload while Play was running cleared the existing Player stamina state and produced null-reference errors; restarting Play restored normal initialization. Final fresh Play sessions were verified separately; edit scripts outside Play. Existing BinaryFormatter and VS/Unity UDP messaging warnings remain outside this task.
Final style selection: the user preferred the complex mixed examples (2, 11 and the cross) and rejected the more linear U/ascent/horizontal examples. Normal Random therefore accepts only mixed exploration with meaningful walking in both horizontal directions and substantial ascent and descent. Explicit debug profiles remain selectable, with their serialized values preserved. This is a geometry policy, not a seed blacklist. The complete generation/traversal validator still rejects obstructed candidates; normal WorldRunSession uses its existing bounded seed retries. In the 0..31 fixed-seed sample, 31 were accepted and 24 was deliberately rejected for collider clearance; 32 normal sequential run generations succeeded without a linear fallback. Six final native runs each collected all 12 fragments, traversed all returning branches and reached Goal. The initial comparison image remains as the user's reference; preferred-terrain.png shows the resulting normal generation policy.