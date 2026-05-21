# Underbrew

A cosy exploration game built in Unity as my final major project for the **MA Indie Game Development** at Falmouth University (GDO751, April 2026).

> *Forage. Brew. Tend.*

Underbrew is a vertical slice centred on a single cohesive gameplay loop: explore a hand-drawn underground world, forage for ingredients, process them at a grinding station, and brew potions at your apothecary. It sits at the intersection of cosy game design and metroidvania-style exploration.

---

## About the Project

The original proposal was more ambitious — combining metroidvania traversal, potion crafting, and shop management. Through development I made the deliberate decision to narrow the scope and deliver one polished, functional loop rather than several incomplete systems. The result is a vertical slice that clearly communicates its core experience.

The art direction also shifted during development. I moved from pixel art to a hand-drawn style, which allowed faster iteration while giving the game a more distinctive visual identity.

---

## Gameplay Systems

All systems were designed and built from scratch:

- **Player controller** — state machine handling movement, jumping, wall sliding, wall jumping, and dashing
- **Inventory system** — item pickup, storage, and slot management
- **Crafting system** — processing station (grinding) and brewing station, driven by scriptable object recipes
- **Journal** — tabbed interface tracking discovered items, unlocked potion recipes, and quest progress; acts as the primary onboarding tool
- **Dialogue system** — conversation assets with progression gating tied to game state flags
- **Save system** — persistent save data across scenes including resource respawn state and checkpoint progress
- **Scene transition manager** — fade-based transitions with spawn point routing
- **Audio manager** — centralised audio library with mixer support for music, ambience, and SFX

---

## Built With

- **Unity 2026 (URP)** — Universal Render Pipeline, 2D
- **C#**
- **Input System** — Unity's new input system
- **Dual Grid Tilemap** — [com.skner.dualgrid](https://github.com/skner-dev/DualGrid) for terrain tiling
- **Cinemachine** — camera framing and transitions

---

## Credits

| Asset | Source |
|---|---|
| Character animations | [Warrior Free Animation Set by Clembod](https://clembod.itch.io/warrior-free-animation-set) |
| Sound effects | [Freesound.org](https://freesound.org/) / [The Spriters Resource](https://sounds.spriters-resource.com/) |
| All artwork, UI, and scene art | Original hand-drawn — Samuel Perkins |

---

## Context

This project was submitted as the major project component of the **MA Indie Game Development** programme at Falmouth University. It represents approximately one academic year of solo development, covering design, programming, art, audio integration, and playtesting.

---

*Samuel Perkins — 2026*
