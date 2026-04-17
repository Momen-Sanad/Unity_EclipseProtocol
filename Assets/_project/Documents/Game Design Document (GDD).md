# Eclipse Protocol \- Game Design Document (GDD)

## Table of Contents

1. Project Identity and Executive Summary  
2. 3D Vision and Core Gameplay Loop  
3. 3D Player Mechanics and Controls  
4. 3D Dash and Survival Systems  
5. 3D AI and NavMesh Navigation  
6. Procedural 3D Environment Generation  
7. Technical Architecture (C\# and Unity)  
8. 3D UI and HUD Design  
9. 3D Visuals, Lighting, and Animation  
10. 3D Spatial Audio and Roadmap  
11. Phase 1 Checklist (Midway)  
12. Final Phase Checklist  
13. Submission Checklist

---

## 1\) Project Identity and Executive Summary

### 1.1 Project Branding

- Project title: Eclipse Protocol  
- Team members (1-2):  
- Course / section: SWGCG351 \- Game Design and Development (Unity 3D Project, Spring 2026\)  
- Instructor: Dr. Mohamed Sami Rakha  
- Contact info:  
- Logo / typography direction: Sci-fi industrial style, clean sans-serif, emergency/station-warning visual language.

### 1.2 Version History

| Version | Date | Author | Changes |
| :---- | :---- | :---- | :---- |
| v0.1 | 2026-04-15 | Rana | Initial draft |
| v0.2 | 2026-04-17 | Momen | Team-specific updates \+ technical constants |
| v1.0 | 2026-04-20 | Rana | Final submission draft |

### 1.3 High Concept

- A top-down 3.5D sci-fi survival strategy game where a maintenance robot must restore power, evade hostile drones, and reach extraction in a failing space station.

### 1.4 Design Pillars (3 max)

- Tension: Constant pressure from hostile drones and resource scarcity.  
- Scarcity: Limited energy/health management drives decisions.  
- Fluidity: Snappy movement, responsive dash, and readable state-based gameplay.

### 1.5 Target Audience and Form of Fun

- Target players: Players who enjoy survival-strategy action, systemic gameplay, and replayable runs.  
- Form of fun: Challenge, discovery, mastery, and adaptation.  
- Why this audience will enjoy this game: The project combines procedural layouts, escalating AI threats, and resource management with short, replayable sessions.

---

## 2\) 3D Vision and Core Gameplay Loop

### 2.1 3D Vision

- The 3D perspective adds depth to exploration, enemy line-of-sight, and spatial navigation inside the failing station.  
- 3D room modules and NavMesh-driven AI make environment layout and movement feel more tactical than a flat 2D grid.  
- Camera style: 3.5D top-down perspective.

### 2.2 Core Gameplay Loop (Diagram Required)

- Loop: Exploration \-\> Resource Collection \-\> Node Repair \-\> Survival/Evacuation  
- Loop description:  
1. Explore generated rooms and corridors.  
2. Collect energy cells and maintain survival resources.  
3. Restore power nodes to progress and stabilize access.  
4. Survive AI pressure and complete extraction.  
- Add a simple flow diagram image or figure reference:

### 2.3 Camera Technical Plan

- Camera position and angle: High-angle top-down (example from brief: Y around 10, X rotation around 60 degrees).  
- Follow behavior: Smooth follow using Cinemachine or SmoothDamp.  
- Clipping/jitter prevention: Maintain collision-safe offset and damped movement updates.

---

## 3\) 3D Player Mechanics and Controls

### 3.1 Input Mapping

| Action | Input | Notes |
| :---- | :---- | :---- |
| Move | WASD | X/Z movement |
| Dash | Space | Consumes energy \+ cooldown |
| Aim / Interaction | Mouse | Aiming/interaction per brief |
| Pause | Esc | Opens pause state |

### 3.2 Rigidbody and Collider Setup

- Player collider type: CapsuleCollider  
- Rigidbody values (mass, drag, angular drag):  
- Constraints (freeze rotation axes, etc.): Freeze unwanted rotation to keep stable top-down control.

### 3.3 Movement Logic

- Movement is physics-driven via `Rigidbody.linearVelocity` or `Rigidbody.AddForce`.  
- Movement updates occur in `FixedUpdate()` to remain frame-rate independent.  
- Sliding prevention: Clamp and normalize movement vector, tune drag/friction, and avoid direct transform teleport movement.  
- Target movement feel: Snappy, responsive, and readable.

---

## 4\) 3D Dash and Survival Systems

### 4.1 Dash Design

- Dash direction rule: Movement vector at dash trigger (can be updated to cursor-based if desired).  
- Dash force: TBD  
- Dash duration: 1 second  
- Dash cooldown: 8 seconds

### 4.2 Damage and I-Frames

- Invulnerability window duration:  
- Enemy collision is ignored during dash using temporary layer collision changes or an invulnerability flag for trigger checks.

### 4.3 Survival Economy

- Health max: 100  
- Energy max: 100  
- Energy cost per dash: 20  
- Energy restore sources: Energy Cell prefabs.  
- Health damage sources: Drone collisions/attacks and environmental threats.  
- Optional health regeneration formula:

---

## 5\) 3D AI and NavMesh Navigation

### 5.1 NavMesh Setup

- Walkable surfaces: Station floors and designated traversable modules.  
- Non-walkable: Walls, blocked geometry, and obstacle volumes.  
- Baking approach:  
  - Phase 1: Baked NavMesh in a static test room.  
  - Final phase: Runtime/dynamic NavMesh update using `NavMeshSurface` for generated levels.  
- Agent settings (speed, acceleration, stopping distance):

### 5.2 Enemy Types

- Patrol Drone: Follows waypoint route and guards space predictably.  
- Hunter Drone: Detects player and actively pursues with FSM transition into chase/attack behavior.

### 5.3 FSM (Flowchart Required)

- States: Idle, Patrol, Chase, Return, Attack.  
- Trigger conditions for each transition:  
  - Patrol \-\> Chase: Player enters detection sphere.  
  - Chase \-\> Attack: Player enters attack range.  
  - Chase \-\> Return/Patrol: Player exits detection range.  
  - Return \-\> Patrol: Drone reaches assigned route/post.  
- Detection method: `Physics.OverlapSphere` and/or raycast line-of-sight validation.  
- Add FSM flowchart image reference:

### 5.4 Combat / Collision Logic

- Attack behavior: Projectile attack or physical dash attack when in attack range.  
- Damage formula:  
- Player hit cooldown / grace period:

---

## 6\) Procedural 3D Environment Generation

### 6.1 Room Prefab Library

| Prefab | Purpose | Anchors |
| :---- | :---- | :---- |
| StartRoom | Safe spawn / onboarding | Entrance, Exit |
| CorridorModule | Transitional movement/combat lane | Entry, Exit |
| NodeRoom | Contains repair objective | Entry, Exit |
| ExtractionRoom | End objective target | Entry, Exit |

### 6.2 Spawner Algorithm

1. Player reaches door trigger.  
2. Select random room prefab from pool.  
3. Validate overlap and placement constraints.  
4. Snap new room entrance anchor to current room exit anchor.  
5. Spawn room contents (drones, energy cells, node objects).  
6. Remove/disable distant geometry if needed for performance.

### 6.3 Seed and Replayability

- Seed source: `int seed = inputSeed.Aggregate(0, (hash, c) => hash * 31 + c);`  
- `System.Random rng = new System.Random(seed);`  
    
- How to reproduce same run: Store and reuse selected seed.  
- What randomizes each run: Room module order, room content placement, enemy/resource distribution.

### 6.4 Runtime Navigation

- Use `NavMeshSurface` components to bake/update navigation at runtime after procedural room instantiation.  
- Constraints/performance notes: Combine with pooling/cleanup strategy to keep stable runtime performance.

---

## 7\) Technical Architecture (C\# and Unity)

### 7.1 Core Script Map

| Script | Responsibility | Depends On |
| :---- | :---- | :---- |
| `PlayerController.cs` | Movement, dash, health, energy | Input, Rigidbody, Animator |
| `DroneAI.cs` | FSM logic \+ navigation \+ attack | NavMeshAgent, target transform |
| `ProceduralLevelGen.cs` | Room spawning and anchor alignment | Room prefab pool, seed logic |
| `GameStateManager.cs` | Menu/pause/play/win/loss transitions | UI manager |
| `HUDController.cs` | Health/energy/cooldown updates | Player stats/events |
| `AudioManager.cs` | Mixer routing and runtime SFX/music control | AudioMixer |

### 7.2 Data Containers

- ScriptableObjects list:  
  - `PlayerStats.asset`  
  - `DroneStats.asset`  
  - `RoomModuleData.asset`  
  - `GameBalanceData.asset`  
- Tunable fields: Movement speed, dash force, cooldowns, detection radius, attack range, health/energy values.

### 7.3 Collision Layer Matrix

| Layer A | Layer B | Collide? | Why |
| :---- | :---- | :---- | :---- |
| Player | Wall | Yes | Physical boundaries |
| Player | Enemy | Yes | Damage and combat checks |
| Enemy | Enemy | Yes | Tune crowding behavior |
| Projectile | Wall | Yes | Stop projectile on impact |
| Projectile | Enemy | Yes | Enable friendly fire as design choice |

### 7.4 State System

- Required states: Menu, Playing, Pause, GameOver, Victory.  
- Transition rules:  
  - Menu \-\> Playing: Start input.  
  - Playing \-\> Pause: Pause input.  
  - Playing \-\> GameOver: Health or energy reaches zero.  
  - Playing \-\> Victory: Player reaches extraction point.  
  - Pause \-\> Playing: Resume.

---

## 8\) 3D UI and HUD Design

### 8.1 UI Strategy

- Screen-space HUD for core stats (health, energy, cooldown, messages).  
- Optional world-space prompts for interactables (node repair, pickups).  
- Reason: Screen-space keeps readability stable during top-down camera movement.

### 8.2 HUD Layout (Mockup Required)

- Health bar position: Top-left (recommended).  
- Energy bar position: Near health bar for paired survival readability.  
- Dash cooldown icon: Near ability area (bottom-right or near energy bar).  
- Mission objective text: Top-center or upper-left objective panel.  
- Optional minimap / compass:  
- Required state messages: Game Over, Victory, pause indicator, node/mission updates.  
- Add HUD mockup reference:

### 8.3 Responsive Behavior

- 16:9 plan: Anchored margins with safe-area padding.  
- Readability checks: Minimum text size, contrast checks, no key UI overlap with camera action zone.

---

## 9\) 3D Visuals, Lighting, and Animation

### 9.1 Visual Style

- Theme keywords: Sci-fi, industrial, failing station, emergency, low-power tension.  
- Materials and color direction: Cool metallic base with danger accents (warning reds/oranges), emissive highlights for critical interactables.

### 9.2 Animation Plan

- Animator states: Idle, Walk, Dash, Death.  
- Blend tree parameter: Movement magnitude/speed.  
- Transition conditions:  
  - Idle \<-\> Walk via speed threshold.  
  - Any \-\> Dash via dash trigger.  
  - Any \-\> Death via health \<= 0\.

### 9.3 Lighting and VFX Plan

- URP setup summary: Use URP for consistent 3D lighting and post-processing.  
- Point/spot/directional lights usage: Directional baseline \+ local point/spot lights in rooms.  
- Post-processing: Bloom \+ vignette for atmosphere.  
- Particle effects: Dash burst, failing wire sparks, drone destruction FX, impact sparks.  
- Feedback polish: Subtle camera shake on dash impact / damage events.

---

## 10\) 3D Spatial Audio and Roadmap

### 10.1 Audio Technical Setup

- AudioMixer groups: Master, Music, SFX.  
- Spatial blend usage: 3D spatial audio for world sounds (target blend near 1.0 for positional sources).  
- Attenuation plan (min/max distance):  
- Pause effect: Optional low-pass filter to create muffled paused soundscape.

### 10.2 Audio Content

- SFX list:  
  - Drone hum  
  - Dash activation  
  - Energy pickup  
  - Node repair interaction  
  - Damage/hit feedback  
  - Drone destruction  
  - UI click/confirm  
- Music list:  
- Priority and volume balancing notes:

### 10.3 Milestone Roadmap

| Milestone | Scope | Target Date | Status |
| :---- | :---- | :---- | :---- |
| Phase 1 Midway | Full GDD \+ 3D prototype baseline | 2026-04-26 | \[ Pending \] |
| Final Phase | Full playable game \+ polish \+ build | 2026-05-03 | \[ Pending \] |
| Bonus | Optional extension | TBD | \[ Pending \] |

---

## 11\) Phase 1 Checklist (Midway \- 30%)

- [ ] 10+ page technical GDD is complete (not a draft).  
- [ ] Architecture mapping is defined (C\# classes and relationships).  
- [ ] Physics constants are defined (mass, drag, dash force values).  
- [ ] AI FSM flowchart is included.  
- [ ] UI mockups are included.  
- [ ] 3D player prototype uses Rigidbody movement in `FixedUpdate()`.  
- [ ] Dash works with energy cost and cooldown.  
- [ ] 3.5D camera follow is implemented.  
- [ ] Collision layers are configured (Player/Environment separation).  
- [ ] Baked NavMesh exists in a static test room.  
- [ ] At least one patrol drone uses `NavMeshAgent` with waypoint logic.  
- [ ] Detection sphere/proximity trigger is verified.  
- [ ] Static 3D sandbox level exists (floors/walls/pillars).  
- [ ] Player/drone/energy-cell are set up as prefabs.

---

## 12\) Final Phase Checklist (70%)

- [ ] Procedural room spawner replaces static sandbox.  
- [ ] Rooms snap without overlap using grid/node/anchor rules.  
- [ ] Seed logic supports randomized or reproducible generation.  
- [ ] Dynamic NavMesh/runtime baking works for generated layouts.  
- [ ] Hunter behavior implemented with FSM state transitions.  
- [ ] Drone chase uses NavMesh shortest-path pursuit.  
- [ ] Attack behavior is implemented (projectile or physical attack).  
- [ ] Energy economy fully integrated (dash consumes, pickups restore).  
- [ ] Win/loss states are implemented (game over and extraction victory).  
- [ ] HUD from GDD is implemented (health, energy, dash cooldown, messages).  
- [ ] VFX polish added (dash/destroy effects).  
- [ ] Camera shake and visual polish applied.  
- [ ] URP/post-processing polish applied.  
- [ ] Stable standalone build is produced with no blocking runtime errors.

---

## 13\) Submission Checklist

### 13.1 Files

- [ ] GDD PDF (10+ pages).  
- [ ] Unity project source.  
- [ ] Playable standalone build (`.exe` or platform equivalent).  
- [ ] `README.md` with setup/run steps.  
- [ ] Asset/source attributions (if applicable).

### 13.2 Platform and Deadline Rules

- [ ] Deliverables uploaded to assigned Google Classroom submission.  
- [ ] Deliverables pushed to project GitHub repository before deadline.  
- [ ] Team representative upload plan confirmed (if team project).  
- [ ] Late policy reviewed (25% daily penalty when allowed; no acceptance after two overdue days).

### 13.3 GitHub Requirements

- [ ] Dedicated repo exists for the project.  
- [ ] Meaningful incremental commits are present.  
- [ ] Commit messages are clear and descriptive.  
- [ ] Repository includes organized structure \+ README.  
- [ ] Commit history demonstrates continuous development.

### 13.4 Demo Prep

- [ ] 2-3 minute walkthrough plan.  
- [ ] Demonstrate phase requirements explicitly.  
- [ ] Be ready to explain architecture and technical decisions.  
- [ ] Be ready to answer evaluation questions live.

---

## Optional Appendix (During Finalization)

- A. FSM flowchart image  
- B. Core loop diagram image  
- C. Class diagram  
- D. HUD wireframe/mockup  
- E. Performance notes and risk list

