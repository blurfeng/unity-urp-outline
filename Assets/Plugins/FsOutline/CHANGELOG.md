# Changelog

All notable changes to the `com.fs.outline` package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-07-08

### Added
- **Expand Mode** — a Volume-overridable setting that selects how the outer outline is expanded from the mask. **Jump Flood (JFA)** builds a true Euclidean distance field and stays smooth and uniform-width at any width (default); **Separable Blur** yields a soft, glow-like edge; **Dilate** is the lightweight 8-tap morphological dilation — cheapest, but turns octagonal at large widths.
- **Occlusion Culling** — a Volume-overridable toggle (with a Feature-level default). When enabled, the parts of an outlined object hidden behind other geometry are no longer outlined. The camera depth texture is requested automatically, only while occlusion is on.
- **Transparent object support** — objects using transparent or alpha-clipped materials now generate correct mask coverage and are outlined properly.

### Changed
- **Edge Hardness** reworked into a power-curve shaping of the edge falloff (larger = sharper/thinner, smaller = softer/thicker); a low value reproduces a uniform, solid outline band.
- Renamed the configurable Feature and Volume fields to PascalCase (`Color`, `Width`, `Opacity`, `Hardness`, `OcclusionCulling`, `ExpandMode`, `RenderingLayerMask`). **Breaking:** references configured under 1.1.0 must be reassigned after upgrading.

### Fixed
- With Occlusion Culling on, the outline no longer bleeds over occluding geometry, and the visible edge is trimmed precisely against the nearer surface. This now applies consistently across all three Expand Modes (Dilate, Jump Flood, Separable Blur) — Jump Flood and Separable Blur previously left a small gap where the outline met an occluder.

### Removed
- **Inner Penetration** parameter — removed. The outline's inner falloff is now shaped by Edge Hardness. **Breaking:** any Feature or Volume override of Inner Penetration no longer applies.

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

[1.2.0]: https://github.com/blurfeng/unity-urp-outline/releases/tag/v1.2.0
[1.1.0]: https://github.com/blurfeng/unity-urp-outline/releases/tag/v1.1.0
[1.0.0]: https://github.com/blurfeng/unity-urp-outline/releases/tag/v1.0.0
