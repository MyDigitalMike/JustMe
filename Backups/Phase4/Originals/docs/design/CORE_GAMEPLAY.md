# JustMe — core gameplay

Confirmed product decisions, Phase 3 (Unity 6000.6.8f1).

- Hard difficulty from the beginning; no automatic easy-mode scaling.
- Atmospheric procedural exploration with almost total darkness: approximately one or two character steps of local visibility, readable silhouette and UI. No persistent illumination of unexplored terrain.
- Walk, sprint, jump, double jump, crouch/crawl, grab/release/pull up ledges, and climb only designated surfaces. No unrestricted wall climbing, wall jump or dash.
- Shared stamina: sprinting, climbing, hanging and crawling drain continuously; double jump and pull-up have discrete costs. Recovery only at grounded rest/walking after exertion delay. Walking remains available at exhaustion; climbing/hanging exhaustion drops the player. Transitions do not refill stamina.
- Horizontal, ascent, descent and mixed worlds. Goal may lie above, below or across from Spawn.
- Each accepted world needs a feasible directed traversal route under measured movement, geometry and stamina constraints. Air connectivity alone is insufficient. Difficult mandatory segments coexist with optional reconnecting paths, backtrackable dead ends, risky shortcuts and explicit lethal hazards.
- Lethal voids lead to GameOver. Ordinary platform falls are not automatically lethal. Danger must be visible before an unavoidable commitment despite the short sight radius.
- No active enemies, coins, combat, inventory or monetization. Preserve their assets; narrative systems are postponed.

Prototype stamina configuration: max100; sprint20/s, climb30/s, hang14/s, crawl8/s; double jump18, pull-up15; idle recovery13/s, walking recovery5/s; recovery delay1.5s. These are tuning data, not automatic difficulty scaling.

Implementation decisions: retain Player GUID and one contact-normal ground authority; a small explicit traversal state plus independent stamina arithmetic; keep generation data and traversal validation pure C#. Plan mandatory routes before natural terrain, preserving reserved clearance and landings. Explicit rest actions model elapsed recovery rather than magically resetting stamina. Use half-unit terrain cells locally so the existing ~0.78-unit standing collider and ~0.39-unit crouched collider support real low tunnels. Preserve the legacy generator path and save format.

Keyboard prototype: A/D or arrows walk; Shift sprint; Ctrl/C crouch/crawl; Space/W jump; Up/Down on designated climbs; jump/Up pulls from a ledge; Down releases it. Existing virtual axes/buttons remain supported. Exact capabilities and conservative generation limits come from the matching Editor tests, not these design intentions.

Hardcore progression uses stamina in the existing HUD and removes the legacy countdown. Designated ropes and red departure marks identify climbable shafts and lethal gaps locally; they do not illuminate the map. Existing three-level scene progression/save layout remains intact.
