# Contributing Guide

## Overview

This document defines how our team collaborates on code. **Taiga is our single source of truth** -- every branch, commit, and pull request must trace back to a Taiga user story, task, or issue. Our Git workflow is designed to integrate directly with Taiga via its native GitHub webhook integration.

Read this document fully before making your first contribution.

---

## Branching Strategy

All branches originate from Taiga items. **No branch should exist without a corresponding Taiga user story, task, or issue.**

### Protected Branches

| Branch    | Purpose                                         | Rules                                              |
| --------- | ----------------------------------------------- | -------------------------------------------------- |
| `main`    | Stable, deployable builds only                  | No direct pushes. PRs require at least 1 approval. |
| `develop` | Integration branch. All work merges here first. | PRs only. No direct pushes.                        |

### Working Branches

Working branches follow this naming convention:

```
<type>/TG-<ref>/<short-description>
```

Where:

- `<type>` is one of:
  
  | Type      | Use for                          |
  | --------- | -------------------------------- |
  | `feature` | New functionality                |
  | `bugfix`  | Bug fixes (maps to Taiga issues) |
  | `asset`   | Model, texture, or SFX work      |
  | `docs`    | Documentation only               |

- `<ref>` is the Taiga reference number

- `<short-description>` is a brief summary

### Examples

```
feature/TG-42/telescope-interaction     -- Implementing the telescope prop (Taiga user story #42)
feature/TG-15/pause-menu-ui             -- Building the pause menu (Taiga task #15)
bugfix/TG-78/falling-through-floor      -- Fixing a collision bug (Taiga issue #78)
asset/TG-30/coffee-mug-model            -- Adding a 3D prop (Taiga task #30)
docs/TG-55/update-setup-instructions    -- Updating documentation (Taiga task #55)
```

> **If the work doesn't have a Taiga item yet, create one first.** Every branch must be traceable.

### Creating a Branch

Always branch from `develop`:

```bash
git checkout develop
git pull origin develop
git checkout -b feature/TG-42/telescope-interaction
```

---

## Commit Messages

Commits use **conventional commit format** with a **mandatory Taiga reference**:

```
<type>(TG-<ref>): <description>
```

### Types

| Type       | Use for                                   |
| ---------- | ----------------------------------------- |
| `feat`     | New feature or functionality              |
| `fix`      | Bug fix                                   |
| `docs`     | Documentation changes                     |
| `style`    | Code formatting (no logic change)         |
| `refactor` | Code restructuring (no behavior change)   |
| `test`     | Adding or updating tests                  |
| `asset`    | Assets (models, textures, materials, SFX) |

### Examples

```
feat(TG-42): add snap-to-fit for puzzle pieces
asset(TG-30): add coffee mug model
fix(TG-78): resolve falling through floor collision
docs(TG-55): update setup instructions
test(TG-42): add unit tests for puzzle snapping
```

### Rules

- `TG-<ref>` is required on all commits tied to a Taiga item.
- For rare commits not tied to a specific Taiga item (e.g., fixing a typo in `.gitignore`), the `TG-` reference can be omitted.
- Keep the description short and imperative: "add feature" not "added feature" or "adding feature".

---

## Pull Request Process

### 1. Before Starting Work

- Ensure a Taiga user story, task, or issue exists for the work.
- Assign yourself to the Taiga item and move it to **In Progress**.
- Create your branch from `develop` using the naming convention above.

### 2. During Development

- Commit frequently with conventional messages including `TG-<ref>` references.
- Push regularly to your remote branch:
  
  ```bash
  git push -u origin feature/TG-42/telescope-interaction
  ```

### 3. When Ready for Review

- Open a Pull Request targeting `develop`.
- **PR title** follows the format: `<type>(TG-<ref>): <description>`
- **PR description** must include:
  - Summary of what was changed and why
  - Testing notes: what was tested, on what device, and any VR-specific observations
- Request at least **1 reviewer** (Git Master reviews all PRs).
- The PR template (`.github/PULL_REQUEST_TEMPLATE.md`) will auto-populate the required fields.

### 4. Review and Merge

- Reviewer checks code quality and that the Taiga reference is valid.
- Resolve any merge conflicts against `develop`.
- Merge the PR once approved.

### 5. After Merge

- Delete the merged feature branch (GitHub can be configured to do this automatically).

---

## Unity-Specific Conventions

### Project Settings for Version Control

These settings are **critical** and must be verified before any work begins:

| Setting                  | Value                  | Location                                                    |
| ------------------------ | ---------------------- | ----------------------------------------------------------- |
| Asset Serialization Mode | **Force Text**         | Edit > Project Settings > Editor > Asset Serialization Mode |
| Version Control Mode     | **Visible Meta Files** | Edit > Project Settings > Editor > Version Control Mode     |

**Force Text** ensures Unity serializes scenes and assets as human-readable YAML, which makes diffs possible. If this is not set, merges will break.

**Visible Meta Files** ensures every asset has a corresponding `.meta` file tracked in Git. Never delete `.meta` files manually.

### Scene Workflow (Avoiding Merge Conflicts)

Unity scenes (`.unity` files) are massive YAML files that merge poorly. The team follows these rules:

1. **Announce in Slack** when you start working on a scene. Only one person edits a scene at a time.
2. **Choose prefab-based development when possible**: Build features as prefabs, then place them in scenes. This minimizes scene file changes.
3. If two people must work on the same scene, one person handles the scene layout and the other works entirely in prefabs. Coordinate in Slack before starting.
4. Consider creating a Taiga task specifically for scene integration work if multiple prefabs need to be placed.

### Naming Conventions

| Item    | Convention             | Examples                                                               |
| ------- | ---------------------- | ---------------------------------------------------------------------- |
| Scripts | PascalCase             | `PlayerController.cs`, `PuzzleEngine.cs`                               |
| Folders | PascalCase             | `Scripts/`, `Prefabs/`, `Materials/`                                   |
| Assets  | PascalCase descriptive | `MissionControlDesk.fbx`, `LaunchButton_Albedo.png`                    |
| Scenes  | PascalCase             | `MainMenu.unity`, `MissionControlRoom.unity`, `ChildhoodBedroom.unity` |

### Code Style

- Follow standard C# conventions and Unity best practices.
- All public methods and complex logic should have XML doc comments.
- No magic numbers -- use constants or ScriptableObjects.
- Use `[SerializeField] private` over `public` fields where possible.
