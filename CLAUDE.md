# CLAUDE.md — Sudoku Killer Project

## Git Conventions

When making git commits, always commit ALL changed/untracked files unless explicitly told otherwise. Use `git add -A` before committing, not just feature-specific files.

## Game Logic / Validation

When implementing validation/rule systems, always check constraints bidirectionally — validate from BOTH the new element's perspective AND all existing elements' perspectives. Never assume one-directional checking is sufficient.

## Unity Workflow

This is a Unity project using the UnityMCP server. Always use `mcp__UnityMCP__read_console` to check for errors after applying script changes or creating GameObjects. Prefer `mcp__UnityMCP__script_apply_edits` for modifying existing scripts. When serialized fields are involved, verify they persist correctly in the Unity Editor (serialization bugs are common).

### Grid System

When interpreting dimensions or sizes for the Unity grid system: cell size means the size of each individual cell, NOT the total grid size. The grid uses 0.48 x 0.48 cell size on a sprite-based overlay. Always confirm dimensional interpretations before implementing.

### Input & Drag-and-Drop

Avoid using OnMouseDown for drag-and-drop input in Unity — use the EventSystem/pointer interfaces (IPointerDownHandler, IDragHandler, IDropHandler) or Input system raycasting instead. OnMouseDown has known issues with overlapping colliders and UI layers.
