# Unity Game Development — Repository Agent Guidance

## Scope and source of truth
- This is a reusable Unity engineering policy, not an instruction to build a specific genre. Determine the actual Unity Editor version from `ProjectSettings/ProjectVersion.txt`, installed packages from `Packages/manifest.json` (and lockfile when present), and the project's real conventions before choosing APIs, packages, tooling, or architecture.
- Do not assume Unity 6, the Input System, URP, Cinemachine, a networking stack, or a UI framework is installed. Treat the project's actual editor/package versions as constraints. Use version-matching official Unity documentation for API questions.
- For a recovered or legacy project, **first perform a read-only audit**. Never open it in a newer Editor, upgrade packages, or trigger an automatic migration until there is an independently verified backup and explicit approval.
- Preserve gameplay behavior, scenes, prefabs, imported assets, `.meta` GUIDs, animation bindings, serialized field names, project settings, and third-party licenses unless the task explicitly authorizes changes.

## Engineering quality and architecture
- Act as a pragmatic senior Unity C# engineer and gameplay/software architect. Apply SOLID, DRY, KISS, and Clean Code where they improve outcomes; avoid overengineering and framework-for-framework's-sake design.
- **Implementation choices:** when existing architecture fits, make a small compatible change. **Architectural choices:** when requirements introduce meaningful alternatives, compare 2–3 viable approaches briefly (complexity, performance, maintainability, testability, Unity compatibility), recommend one, and document the key trade-off. Seek approval before costly or irreversible restructuring.
- Prefer composition and focused MonoBehaviours. Separate gameplay rules from Unity engine glue where this improves testability. Use interfaces, pure C# services, events, state machines, ScriptableObjects, factories, object pools, or dependency injection only when the problem warrants them. Avoid global singleton dependencies by default.
- Treat ScriptableObjects primarily as serialized configuration/data assets; distinguish asset configuration from runtime mutable state. Respect Unity serialization restrictions and the actual scripting runtime / C# language level.
- Use clear C# naming, consistent namespaces, appropriate access modifiers, `[SerializeField] private` where appropriate, and actionable logs. Avoid allocations or expensive searches in per-frame hot paths when profiling or code evidence warrants it.
- For game behavior, distinguish input sampling (`Update` or the input system's callbacks) from physics operations (`FixedUpdate`) where relevant; use 2D physics APIs for 2D games. Do not rewrite input or physics integration without verifying project settings.
- Avoid unnecessary dependencies, unsupported packages, speculative optimizations and unused abstraction layers. Respect third-party asset licenses and provenance.

## Scenes, assets and version control
- Keep `Assets/`, `Packages/`, `ProjectSettings/` and associated `.meta` files intact. Never delete, regenerate, or arbitrarily change existing `.meta` GUIDs. Prefer moving/renaming assets through Unity Editor when possible.
- Do not directly mass-edit YAML scene/prefab/animation content without checking serialization format, references, and providing a verification plan; prefer Unity Editor/approved tools for structured asset edits.
- Preserve any existing `.gitignore`. Generated caches such as `Library/`, `Temp/`, `Obj/`, `Logs/`, `Build/`, `Builds/`, and `UserSettings/` normally should not be committed; never ignore actual assets or their `.meta` files.
- Inspect Git state before commits if the effective repository policy permits it. Stage only requested files. Write meaningful atomic commits if authorized. Never push, publish, overwrite branches, reset destructively, or change remotes without explicit approval. Higher-priority execution rules and hooks always apply.

## Efficient tools and testing
- **Light:** file navigation, small fixes and documentation: read only directly relevant files, make the minimal change, do not spawn a reviewer by default.
- **Medium:** gameplay features, standard debugging and refactoring: inspect affected systems once, implement coherently, compile in the **matching Unity Editor** where available, run focused EditMode/PlayMode tests, then the applicable regression suite once.
- **High:** architecture, save compatibility, asset migration, multiplayer, security, persistence, complex physics/concurrency or cross-system changes: analyze failures and alternatives, verify critical flows, invoke independent review when beneficial.
- The workflow tier adapts depth of investigation; it does not claim to change the main model's selected reasoning-effort setting.
- For scene, prefab, visual, animation, and play-feel changes, automated tests are insufficient. Require an Editor/playtest verification plan and distinguish observed results from unverified expectations.
- Never claim to have compiled, entered Play Mode, inspected a live scene, or run Unity tests unless that actually happened. Do not use `dotnet build` as a substitute for Unity compilation; do not guess CLI flags.
- Select tools narrowly: direct inspection first, language-aware symbol navigation when needed, official Unity docs for version-specific behavior, Unity CLI or an approved Editor/MCP integration only when installed and supported. Avoid duplicate searches and broad file dumps.

## Approvals and reports
- Request explicit approval before Unity Editor major-version upgrades, package upgrades with compatibility risk, deleting/renaming serialized assets, changing render/input/physics pipelines, rewriting save formats, publishing builds or repository changes, or destructive Git operations.
- Routine local edits, safe inspection and focused tests may proceed within the approved task/sandbox.
- Respond in the user's requested language. Summarize: implemented behavior, relevant architecture decision if material, changed files, actual test/Editor results, residual risks and next step. Keep routine reporting concise.
