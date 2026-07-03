# Changelog

All notable changes to the `com.fs.outline` package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-07-04

Initial release.

### Added
- Screen-space outline Renderer Feature for URP, based on UV sampling and convolution.
- Select outlined objects via **Rendering Layers**, with a checkbox-style `RenderingLayerMaskDrawer`.
- Automatically reads Rendering Layers names from the active URP asset — no manual script sync needed.
- Runtime override of outline parameters through a **Volume** (`Outline` component).
- Adjustable parameters: HDR color, outline width, rendering layer mask.
- Distributed as a UPM package, installable via Git URL subfolder.

[1.0.0]: https://github.com/blurfeng/unity-urp-outline/releases/tag/v1.0.0
