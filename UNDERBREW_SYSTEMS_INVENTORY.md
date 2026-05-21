# Underbrew Systems Inventory

## Summary
Underbrew currently has a playable core loop built around traversal, interaction, dialogue, inventory, basic crafting, persistence, and scene flow. Most of the player-facing foundation is in and functional. A few systems are intentionally disabled, placeholder-level, or only partially content-complete.

## Core Gameplay
- **Player movement and traversal** — Working  
  Side-scrolling movement, jump, coyote time, jump buffer, wall slide, wall jump, dash, and movement tuning are implemented through the player state machine.
- **Dash system** — Working  
  Dash duration, speed, and cooldown are implemented and inspector-editable.
- **Attack/combat states** — Present but currently disabled  
  Basic attack and jump attack states still exist in code, but attack input is now gated behind an editable `allowAttackInput` toggle and is disabled by default.
- **Interaction system** — Working  
  The player detects nearby `IInteractable` targets, shows prompts, and interacts using press input rather than hold input.

## Interaction and World Systems
- **Pickups and one-shot pickups** — Working  
  Standard pickups and persistent one-shot pickups exist, including flag-based consumed state and optional post-pickup dialogue/flag effects.
- **Bench checkpoints** — Working  
  Benches act as interactables, update checkpoint state, and can save immediately.
- **Dialogue interactables and triggers** — Working  
  NPC/object conversations, area-based dialogue triggers, intro dialogue triggers, and flag-gated one-time conversations are implemented.
- **Scene entrances/exits** — Working  
  Scene transitions use entrances, exits, and new-game spawn points to place the player correctly.
- **Ending sequence trigger** — Partly functional  
  There is an ending flow with blackout, dialogue, UI suppression, and return-to-menu behavior, but it should be treated as a scripted sequence rather than a broadly reusable system.

## Inventory, Backpack, Brewing, and Processing
- **Inventory system** — Working  
  Slot-based inventory supports add/remove, save/load snapshots, slot moves, requirement checks, and crafting consumption logic.
- **Backpack UI** — Working  
  The backpack opens, closes, reflects inventory contents, supports dragging/reordering, respects unlock flags, and correctly blocks itself while journal/dialogue/crafting modal systems are active.
- **Brewing system** — Working  
  Brewing stations accept two ingredients, match recipes, show output preview, run timed brewing, update progress, apply post-brew flags, and optionally queue follow-up dialogue.
- **Processing system** — Working  
  Processing stations accept one input item, resolve recipes, run timed processing, apply progress, and can set quest/progression flags on successful completion.
- **Progress bars in crafting UIs** — Working  
  Brewing and processing progress bars are hidden unless an active process is underway.
- **Crafting station abstraction** — Working  
  `CraftingStation` routes interaction to brewing or processing UI depending on station setup.

## Dialogue, Journal, and Progress Tracking
- **Dialogue system** — Working  
  Conversation assets, nodes, lines, choices, conditions, outcomes, auto-advance handling, and UI rendering are implemented.
- **Dialogue outcomes** — Working  
  Dialogue can set flags, add items, and remove items.
- **Journal UI** — Working  
  Journal opens and closes, uses tabs, blocks gameplay input via modal locking, and coexists correctly with backpack and crafting restrictions.
- **Journal item discovery** — Working  
  Items can be discovered and persisted for the journal.
- **Potion recipe discovery** — Working  
  Potion and recipe discovery is tracked separately and saved.
- **Quest journal page** — Partly functional  
  Quest entries, visibility and completion flags, and step display are implemented, but overall usefulness depends on how much quest content has been authored.

## Saving, Persistence, and Runtime State
- **Save system** — Working  
  Save files serialize scene, checkpoint, inventory, flags, resource respawn state, and journal discovery state.
- **Continue flow** — Working  
  Boot flow can detect and restore a valid save into the saved scene.
- **New Game flow** — Working  
  New Game clears save data and resets persistent runtime state, including flags, inventory, checkpoint state, discovery state, and related session data.
- **Game state flags** — Working  
  Central flag manager supports defaults, runtime updates, save snapshots, and inspector/debug viewing.
- **Checkpoint manager** — Working  
  Tracks active checkpoint ID, scene, and position and restores from save data.
- **Resource respawn state** — Working  
  Tracks cooldown-style respawn timing for persistent resource nodes.
- **Runtime persistence reset** — Working  
  A dedicated reset path exists for starting a truly fresh game without stale in-memory session state.

## Scene Flow, Menus, and Bootstrap
- **Managers root bootstrap** — Working  
  A persistent managers root auto-creates or maintains key global systems like dialogue, flags, save systems, transitions, audio, and diagnostics.
- **Boot scene controller** — Working  
  Handles new game vs continue launch mode and routes into gameplay with correct spawn behavior.
- **Scene transition manager** — Working  
  Handles fade transitions, placement, menu/gameplay cleanup, and persistent flow between scenes.
- **Main menu** — Working  
  Supports new game, continue, settings panel behavior, and boot-scene handoff.
- **In-game menu** — Working  
  Supports pause open/close behavior, save, and quit to menu with modal lock awareness.
- **Persistent camera** — Working  
  Persistent camera survives scene loads and snaps to the player after transitions.
- **Persistent UI root** — Working  
  Gameplay UI can persist across scenes while avoiding menu-scene persistence conflicts.

## Audio
- **Audio manager and cue library** — Working  
  Supports UI, SFX, ambience, and music playback from a centralized cue system.
- **Scene-based ambience/music routing** — Working  
  Audio changes automatically by scene.
- **Transition-safe UI audio** — Working  
  UI transition clicks and shared UI playback cleanup are implemented.
- **Footstep audio** — Working  
  Player footsteps route through the audio system.
- **Audio listener handling** — Partly functional  
  Overall audio works, but there has been at least one transition-time warning about missing audio listeners; this appears to be a polish issue rather than a gameplay blocker.

## Debug and Editor Tooling
- **Custom player inspector** — Working  
  Movement, dash, combat toggle, interaction tuning, collision checks, and runtime debug values are exposed in the editor.
- **GameStateFlags editor** — Working  
  Provides runtime/default flag visibility and filtering.
- **Audio library editor** — Working  
  Supports authoring the centralized audio library.
- **Persistence diagnostics** — Working  
  Logs and inspects persistent manager/runtime state across scene loads.
- **Persistence ID audit tool** — Working  
  Audits save IDs, recipe IDs, checkpoints, and one-shot persistence keys for duplication or missing values.
- **UI raycast probe** — Working  
  Debug utility exists for inspecting UI raycast hits.

## Current Notable Partial or Disabled Areas
- **Combat gameplay** — Disabled intentionally  
  Attack logic and states exist, but attack input is off by default because combat is not in active use.
- **Quest/content completeness** — Partial  
  The systems for quest visibility and completion exist, but usefulness depends on how many quests and journal entries are authored.
- **World drop logic from backpack drag-out** — Placeholder  
  Backpack drag-out currently logs intent rather than spawning a dropped world item.
- **Some transition/audio edge-case cleanup** — Partial polish  
  The main transition flow is functional, but a few warning-level edge cases still exist and are not fully cleaned up.

## Assumptions
- This inventory treats implemented code and wired runtime behavior as "in," even if some systems are still content-light.
- "Partly functional" means the system is real and usable, but either not fully polished, not fully content-authored, or still has placeholder edges.
