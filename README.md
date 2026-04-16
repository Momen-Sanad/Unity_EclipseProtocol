# Eclipse Protocol (Unity 3D)

Eclipse Protocol is a top-down sci-fi survival strategy game set inside a failing space station.  
You play as a maintenance robot trying to restore critical power systems, survive hostile drones, and reach extraction before systems collapse.

## Overview
The project focuses on clean architecture, physics-based player control, procedural level flow, and state-driven enemy AI.

## Core Gameplay
1. Explore connected station rooms.
2. Collect energy resources.
3. Repair damaged power nodes.
4. Survive escalating drone pressure.
5. Reach the extraction point to win.

### Lose Conditions
- Health reaches zero.
- Time deadline passes

## Key Features
- 3D Rigidbody player movement with responsive top-down control.
- Dash ability with cooldown, energy cost, and temporary invulnerability (I-frames).
- Enemy AI using finite state machines (Patrol, Chase, Return, Attack).
- NavMesh pathfinding for reliable navigation around obstacles.
- Procedural room spawning with modular prefabs and anchor-based placement.
- HUD with real-time health, energy, cooldown, and mission feedback.
- URP lighting, VFX feedback, and spatial audio routing.

## Tech Stack
- Engine: Unity (3D)
- Language: C#
- Rendering: URP
- AI: `NavMeshAgent` + FSM logic
- UI: Unity Canvas
- Audio: Unity Audio + AudioMixer
- Version Control: Git + GitHub

## Controls
| Action | Keyboard/Mouse
|---|---|
| Move | WASD
| Dash | Space
| Interact | E
| Pause | Esc

## Project Structure
```text
Assets/
  Scenes/
  _project/
    Audio/
    Documents/
    Prefabs/
      Rooms/
      Enemies/
      Gameplay/
    Materials/
    VFX/
    ScriptableObjects/
    Scripts/
      Core/
      Player/
      AI/
      World/
      UI/
      Audio/

```

## Getting Started
### Prerequisites
- Unity Editor: v6.4
- Git

### Run in Editor
1. Clone this repository.
2. Open the project with Unity Hub.
3. Open the main gameplay scene.
4. Press Play.

### Build
1. Open `File -> Build Settings`.
2. Select target platform.
3. Add required scenes in build order.
4. Build and run.


## Documentation
- Course GDD: [`Game Design Document (GDD).md`](./Assets/Documnets/Game%20Design%20Document%20(GDD)%20Template.md)


## Team
- Member 1: Momen Mahmoud
- Member 2: Rana Dief

## Contribution Guidelines
- Keep commits small, meaningful, and frequent.
- Use descriptive commit messages.
- Maintain modular code organization.
- Document major system changes in the GDD or project notes.

## License
  Creative Commons CC0 1.0 Universal