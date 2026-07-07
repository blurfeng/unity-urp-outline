![](Documents/OutlineDemo.gif)

<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/blurfeng/unity-urp-outline?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/blurfeng/unity-urp-outline/total?color=green">
  <img alt="GitHub Repo License" src="https://img.shields.io/badge/license-MIT-blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/blurfeng/unity-urp-outline?color=yellow">
</p>

<p align="center">
  🌍
  <a href="./README.md">中文</a> |
  English |
  <a href="./README_JA.md">日本語</a>
</p>

<p align="center">
  📥
  <a href="#installation">Installation</a> |
  <a href="#download">Download</a>
</p>

# Unity URP Outline
This project implements an outline effect based on **Unity 2022.3.62f3** with the **URP (14.0.12)** rendering pipeline.  
The shader achieves the outline effect using **UV sampling and convolution calculation**.  

## Installation
The outline feature lives in the repository subfolder `Assets/Plugins/FsOutline` and is shipped as a UPM package. It depends on **Universal RP 14.0.12** (`com.unity.render-pipelines.universal`), so make sure URP is enabled in your project.

### Option 1: Install as a UPM package (Recommended)
In your project, open **Window → Package Manager**, click **[+] → Add package from git URL** in the top-left corner, and enter:

```
https://github.com/blurfeng/unity-urp-outline.git?path=/Assets/Plugins/FsOutline
```

> 💡 You can append `#<branch-or-tag>` to the URL to lock a specific version, e.g. `...FsOutline#main`.

### Option 2: Clone the whole repository as a sample project
This repository is itself a fully configured sample project. Clone it and open it with **Unity 2022.3.62f3** to explore the complete outline setup and demo scene.

```
git clone https://github.com/blurfeng/unity-urp-outline.git
```

## Download
Besides installing via UPM, you can also download a package from GitHub Releases and import it manually.

Go to the [Releases](https://github.com/blurfeng/unity-urp-outline/releases) page, download the latest `.unitypackage`, then import it in Unity via **Assets → Import Package → Custom Package...**.

> 💡 A manually imported package won't update automatically with the repository. For version management, prefer the UPM method above.

## How to Use
This project has already been preconfigured. You can use it directly or follow the steps below to integrate the outline effect into your own project.

### 1. Add Renderer Feature
In the **Universal Renderer Data** you are using, click the **[Add Renderer Feature]** button and add the Outline Renderer Feature.  
In the Inspector, configure the parameters. The **Rendering Layer Mask** determines which rendering layer objects will have outlines.

![](Documents/RendererFeature.png)

### 2. Set the Rendering Layer Mask for Target Objects
In the target object's **Mesh Renderer → Additional Settings → Rendering Layer Mask**, assign its rendering layer.  
Match the rendering layer to the **Rendering Layer Mask** set in the Renderer Feature.  

🎉 Once configured, you will see the outline applied to the object.

![](Documents/RenderingLayerMask.png)

### 3. Modify Outline Parameters at Runtime with Volume
If you want to modify the outline effect at runtime, add an **Outline** component to a **Volume** and enable overrides.  
The settings in the Volume will then override the default configuration on the Renderer Feature.

![](Documents/Volume.png)

## Supported Parameters
- **Expand Mode**: Selects how the outer outline is expanded from the mask. **JumpFlood** (default) — a true Euclidean distance field, smooth and uniform-width at any width; **SeparableBlur** — a soft, glow-like edge; **Dilate** — a lightweight 8-tap dilation, cheapest but turns octagonal at large widths.  
- **HDR Color**: The outline color in HDR, which can also create glowing effects.  
- **Outline Width**: Implemented via UV sampling; the adjustable range has been widened (up to 0.05). Under JumpFlood / SeparableBlur large widths stay smooth; only Dilate turns octagonal around sharp corners or square edges.  
- **Opacity**: Overall outline opacity (0–1).  
- **Edge Hardness**: A power-curve shaping of the edge falloff (larger = sharper/thinner, smaller = softer/thicker); a low value reproduces a uniform, solid outline band.  
- **Occlusion Culling**: When enabled, parts of an outlined object hidden behind other geometry are no longer outlined (the camera depth texture is requested automatically, only while this is on).  
- **Rendering Layer Mask**: Controls which objects are outlined through rendering layers.  
- **Render Pass Event**: The pipeline stage at which the outline is drawn. A Renderer Feature-level setting (not overridden per-Volume at runtime); defaults to before post-processing.  

> 💡 Objects using transparent or alpha-clipped materials are now outlined correctly.  
> 💡 Expand mode, color, width, opacity, edge hardness, occlusion culling, and rendering layer mask can all be overridden at runtime via a **Volume**; Render Pass Event is a **Renderer Feature**-level setting.

### Expand Mode Comparison
The image below compares the three expand modes at a large width: Dilate turns octagonal, JumpFlood stays smooth and uniform-width, and SeparableBlur gives a soft glow edge.

![](Documents/ExpandMode.png)

## Rendering Layers Explanation
URP provides **Rendering Layers** to control and distinguish rendering layers.  
It is **recommended to use Rendering Layers instead of Unity's built-in Layer**, for the following reasons:

- **Layer** is also used for physics collisions, camera culling, and other features, which may cause conflicts when mixed with outlines.  
- **Rendering Layers** are independent of physics and logic, making the configuration clearer and more focused.  

You can configure **Rendering Layers** in the **Universal Render Pipeline Global Settings**:  

![](Documents/RenderingLayers.png)

✅ **Automatic Rendering Layer Reading**  
This package automatically reads the configured Rendering Layers names from the active **URP asset**.  
The accompanying `RenderingLayerMaskDrawer` draws the rendering layer mask as a checkbox dropdown whose options are exactly the layers configured in the **Global Settings** above.  
As a result, any change you make to the Rendering Layers is reflected in the options automatically — no manual script synchronization is needed.  