# Project Overview

This is a 3D detective investigation game developed in Unity.

The project is currently in the whitebox / prototype stage.

Primary goal:
Validate the investigation and deduction gameplay loop before visual polish.

## Core Gameplay Direction

The game focuses on:

- environmental investigation
- dialogue with suspects
- evidence collection
- testimony analysis
- time-related investigation mechanics
- memory recall
- deduction

Current planned player-facing systems include:

- Investigation
- Dialogue
- Evidence
- Testimony Board
- Hint
- Time Anchor
- Memory Recall
- Map

These systems are still under development and may change.

## Development Principles

- Prefer simple and testable implementations.
- Avoid over-engineering during prototype development.
- Prioritize gameplay validation over visual polish.
- Keep systems modular so they can be changed later.
- Do not assume unfinished game design decisions are final.
- Ask or use the simplest flexible implementation when requirements are unclear.

## Unity Stack

Current project uses:

- Unity
- URP
- C#
- Input System
- Cinemachine
- ProBuilder
- Git / GitHub
- Unity MCP

## Coding Guidelines

Prefer:

- small focused components
- clear naming
- reusable systems
- data-driven design where appropriate
- ScriptableObject for reusable gameplay data when useful

Avoid:

- unnecessary large frameworks
- premature optimization
- deeply coupled systems
- hardcoded dependencies between gameplay systems

## Prototype Priority

Current priority is to build a small playable investigation prototype.

Focus first on:

1. player movement
2. interaction
3. investigation
4. dialogue
5. evidence acquisition
6. one simple deduction loop

Visual polish and advanced animation should come later.
