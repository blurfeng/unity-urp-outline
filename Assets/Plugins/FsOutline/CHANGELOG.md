# Changelog

All notable changes to the `com.fs.outline` package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-04

### Added
- Three outline parameters, overridable at runtime through the **Volume**: **Opacity** (0–1), **Edge Hardness** (power-curve shaping — >1 sharper/thinner, <1 softer/thicker), and **Inner Penetration** (how deep the outline bleeds into the object).
- **Render Pass Event** — a Renderer Feature-level setting controlling the outline injection stage (defaults to before post-processing; not overridden per-Volume).
- Tooltips on every configurable Feature and Volume parameter.

### Changed
- Widened the outline width range — maximum raised from 0.01 to 0.05 (default unchanged).
- Renamed the configurable fields to drop the redundant `Outline` prefix (`color`, `width`, `opacity`, `hardness`, `penetration`, `renderingLayerMask`). **Breaking:** Renderer Feature and Volume overrides configured under 1.0.0 must be reassigned after upgrading.
- Unified all types under the `Fs.Outline` / `Fs.Outline.Editor` namespaces. Update any external code that referenced the previous namespaces.
- The Volume component editor now records Undo and uses change checks, so edits to the rendering layer mask are undoable.

### Fixed
- Fixed a Material and RTHandle leak — resources are released before recreation in `Create()` and disposed in `Dispose()`.
- Edge detection now runs on the mask's alpha (coverage) channel instead of color, so pure-blue, green, or dark objects are outlined correctly.
- Outline opacity is no longer clamped to 0.5 internally; the color's full alpha is respected.
- Preview and Reflection cameras are skipped, avoiding needless render-target allocation and preview fringing.

### Removed
- A redundant rendering-layer-mask check and an empty `OnCameraCleanup`.

## [1.0.0] - 2026-07-04

Initial release.

### Added
- Screen-space outline Renderer Feature for URP, based on UV sampling and convolution.
- Select outlined objects via **Rendering Layers**, with a checkbox-style `RenderingLayerMaskDrawer`.
- Automatically reads Rendering Layers names from the active URP asset — no manual script sync needed.
- Runtime override of outline parameters through a **Volume** (`Outline` component).
- Adjustable parameters: HDR color, outline width, rendering layer mask.
- Distributed as a UPM package, installable via Git URL subfolder.

[1.1.0]: https://github.com/blurfeng/unity-urp-outline/releases/tag/v1.1.0
[1.0.0]: https://github.com/blurfeng/unity-urp-outline/releases/tag/v1.0.0
