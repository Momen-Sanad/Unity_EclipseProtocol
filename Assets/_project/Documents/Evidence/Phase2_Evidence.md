# Eclipse Protocol - Phase 2 Evidence Pack

This folder tracks the Phase 2 source deliverable. The standalone executable is intentionally skipped for this phase.

## Implemented Coverage
- Separate Menu, Gameplay, Victory, and Loss scenes are wired in build settings.
- Menu accepts an optional seed and stores it through the runtime seed bridge.
- Gameplay generates a seeded station run with Start, Corridor, Node, and Extraction room modules.
- Generated rooms use anchors, bounds, spawn points, objective sockets, seeded selection, overlap checks, and runtime NavMesh rebuild.
- The run has a configurable 180-second timer, repair objective, locked extraction, health loss, timer loss, and extraction victory.
- Gameplay pause is an overlay in the Gameplay scene.
- HUD shows health, energy, dash cooldown, objective text, repair progress, timer, and pause state.
- Hunter behavior now uses patrol, chase, wind-up, lunge, recover, and return states.
- Hunter lunge damage is only enabled during the lunge window and respects player dash invulnerability through `PlayerController`.
- Feedback pass includes dash, pickup, damage, repair, hunter attack, locked extraction, victory, and loss audio hooks plus node/extraction particles.

## Verification Status
- Unity compile/import check passed on May 7, 2026 after the Phase 2 scene, prefab, UI, AI, and feedback assets were wired.
- Unity console check immediately after that import reported 0 errors and 0 warnings.
- Static source review confirms the final generator cleanup patch keeps the spawned player under the generated level root for repeatable seed validation.
- Final deterministic run validation and screenshot capture were attempted, but the Unity editor bridge revoked access before the command could execute.

## Pending Editor Evidence
When the editor bridge is reauthorized, run the Phase 2 validation pass to capture:
- `phase2_gameplay_view.png`: camera view of the generated station run.
- `phase2_generated_layout.png`: top-down view of the seeded room layout.
- `phase2_verification_report.txt`: deterministic seed, non-overlap, NavMesh route, objective spawn, victory/loss/pause checklist results.

## Primary Assets
- Scenes: `Assets/Scenes/Menu.unity`, `Assets/Scenes/Gameplay.unity`, `Assets/Scenes/Victory.unity`, `Assets/Scenes/Loss.unity`
- Balance: `Assets/_project/ScriptableObjects/GameBalanceData.asset`
- Room prefabs: `Assets/_project/Prefabs/Rooms`
- Objective prefabs: `Assets/_project/Prefabs/Gameplay/PowerNode.prefab`, `Assets/_project/Prefabs/Gameplay/ExtractionTrigger.prefab`
- Phase 2 scripts: `Assets/_project/Scripts/Core`, `Assets/_project/Scripts/World`, `Assets/_project/Scripts/UI`, `Assets/_project/Scripts/AI`, `Assets/_project/Scripts/Audio`
