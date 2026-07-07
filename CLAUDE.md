# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**语言要求：必须始终使用中文与用户沟通。**
所有面向用户的输出——回复、解释、计划、提问、总结、待办列表、错误说明等——一律使用中文，且贯穿整个会话，不得中途切换回英文。
内部思考过程可以使用英文节省token，但呈现给用户的任何内容都必须是中文。

---

## 协作规则

- 修改代码前，先简要说明思路，不要直接上来就写代码
- 当存在多种实现方案时，列出各方案让我选择，而不是自行擅自决定
- 绝不自动执行任何 git 操作（commit、push、branch、reset 等）；这些由用户自行处理。即便没有明确指示，也不要主动提议或代替用户执行
- 绝不手工创建 Unity 的 `.meta` 文件，交给 Unity 自动生成，但允许修改已有的 `.meta` 文件。只创建/编辑源资源（如 `.cs`），再让编辑器导入

---

## 仓库定位

本仓库有双重身份：

1. **一个配置完整的 URP 示例工程**（Unity **2022.3.62f3** + URP **14.0.12**），克隆后可直接用 Unity 打开查看描边配置与 `Assets/Scenes/SampleScene.unity` 演示场景。
2. **一个 UPM 包**，位于子目录 `Assets/Plugins/FsOutline`（包名 `com.fs.outline`），是本项目真正对外分发的产物。可通过 Git URL 子目录安装：
   `https://github.com/blurfeng/unity-urp-outline.git?path=/Assets/Plugins/FsOutline`

**几乎所有实质性的代码改动都发生在 `Assets/Plugins/FsOutline/` 内。** 仓库其余部分（`Assets/Scenes`、`Assets/Settings`、`ProjectSettings` 等）只是承载该包的示例宿主工程。

## 构建 / 测试 / 开发

这是一个 Unity 工程，没有命令行构建或测试流程：

- **编译与验证**：用 **Unity 2022.3.62f3** 打开仓库根目录，Editor 会自动编译 `Assets/Plugins/FsOutline` 下的两个程序集。检查 Console 是否有编译错误即为主要的验证手段。
- **没有自动化测试**（工程引入了 `com.unity.test-framework`，但没有任何测试 asmdef 或测试文件）。不要臆造测试命令。
- **手动验证描边效果**：打开 `Assets/Scenes/SampleScene.unity`，进入 Play 或直接在 Scene 视图观察被描边物体。运行时可在场景 Volume 的 Outline 组件中改参数即时预览。
- **改包版本**：同步更新 `Assets/Plugins/FsOutline/package.json` 的 `version` 与 `CHANGELOG.md`（遵循 Keep a Changelog + SemVer）。

## 核心架构

描边是**屏幕空间（后处理式）**效果，通过一个自定义 `ScriptableRendererFeature` 实现。数据与渲染流向如下：

```
OutlineFeature (ScriptableRendererFeature)   ← 挂在 Universal Renderer Data 上
  └─ OutlinePass (ScriptableRenderPass)       ← 每相机注入渲染队列
       1. 把「命中 Rendering Layer Mask 的物体」绘制到一张 Mask RT（ARGB32，只用 alpha 覆盖度）
       2. 用 Outline.shader 画一个全屏三角形：对 Mask 的 alpha 做 Sobel 卷积得到边缘，
          叠加内部衰减与不透明度，混合回相机颜色目标
```

关键文件：

- `Runtime/OutlineFeature.cs` — Feature + 内嵌的 `OutlinePass`。承载生命周期、材质创建、Pass 注入、`UpdateSettings()` 设置合并。
- `Runtime/OutlineSettings.cs` — Feature 级**默认**参数（`[Serializable]`，序列化在 Renderer Data 上）。
- `Runtime/Outline.cs` — `Outline : VolumeComponent`，运行时**覆盖**参数（挂在场景 Volume Profile 上）。
- `Resources/Outline.shader` — 全屏 Sobel 描边 shader；边缘检测采样的是 Mask 的 **alpha（覆盖度）** 而非颜色，故纯蓝/绿/暗色物体也能正确描边。
- `Runtime/Utility/`、`Editor/` — Rendering Layer Mask 相关（见下）。

### 设置合并（默认 vs Volume 覆盖）

`OutlinePass.UpdateSettings()` 每帧执行，逐参数决定取值：当场景 Volume 中的 `Outline` 组件 `isActive` 且该参数 `overrideState==true` 时用 Volume 的值，否则回退到 `OutlineSettings` 默认值。因此运行时改参数只需改 Volume。**例外**：注入时机 `renderPassEvent` 是 Feature 级设置，不参与 Volume 覆盖。新增可覆盖参数时，需要在 `OutlineSettings`、`Outline`、`Outline.shader` 和 `UpdateSettings()` 四处对应地加字段。

### Rendering Layer Mask 与 Unity 版本适配

包同时支持 Unity 2022.3 与 Unity 6，靠 `#if !UNITY_6000_0_OR_NEWER` 分叉：

- **Unity 2022.3**：引擎没有 `RenderingLayerMask` 类型，故提供兼容垫片 `Fs.Outline.RenderingLayerMask`（`Runtime/Utility/RenderingLayerMask.cs`，内部就是一个 `uint`，与 `uint` 隐式互转）+ 自定义 `RenderingLayerMaskDrawer`（`Editor/`）绘制勾选式遮罩下拉。
- **Unity 6（2023.1+ / URP 16）**：`OutlineSettings.cs` 顶部的 `using RenderingLayerMask = UnityEngine.RenderingLayerMask;` 切换到引擎内置类型；垫片与其 Drawer 两个文件都被 `#if` 排除，改用引擎自带绘制器。

层名不是硬编码：`Editor/RenderingLayerMaskGUI.cs` 从当前生效的 URP 资产（`UniversalRenderPipelineAsset.renderingLayerMaskNames`）读取，Global Settings 里增删渲染层会自动反映到下拉选项。

> ⚠ 垫片**故意不定义** `==`/`!=` 运算符：定义后会与 `uint` 内置比较产生二义（CS0034）。只保留隐式转换即可。

## 生命周期健壮性（改 `OutlineFeature` 时务必保留）

`OutlineFeature.cs` 里几处非显而易见的处理，是为修复 Unity 资源导入时序问题而存在的，不要随手删：

- `Create()` 可能在未 `Dispose` 的情况下被反复调用（Inspector 改设置触发 `OnValidate`）→ 先释放旧的 `Material`/`RTHandle` 再重建，防泄漏。
- Shader 引用在首次导入工程时可能暂为空 → 从 `Resources.Load<Shader>("Outline")` 兜底加载。
- `AddRenderPasses()` 里惰性补建：若 `Create()` 早于 Shader 导入完成而没建出材质，会在此再试一次，避免「必须先运行一次描边才出现」。
- `Execute()` 开头 `if (_outlineMaterial == null) return;` — 材质可能在入队后、执行前被销毁。
- 跳过 `Preview` / `Reflection` 相机，避免无谓 RT 分配与预览杂边。

## 程序集与命名空间

- 运行时：`FsOutline.Runtime.asmdef`，命名空间 `Fs.Outline`。
- 编辑器：`FsOutline.Editor.asmdef`（仅 Editor 平台），命名空间 `Fs.Outline.Editor`。
- 两者都引用 `Unity.RenderPipelines.Core.*` 与 `Unity.RenderPipelines.Universal.*`。

## 约定

- **行尾 CRLF**（`autocrlf=true`）。用 Edit/Write 工具改文本，不要用 sed 之类做行尾手术。
- 提交信息与代码注释使用中文，风格见近期 git log（如 `【Bugfix】...`）。
- 面向用户的文档是多语言的：`README.md`（中）/ `README_EN.md`（英）/ `README_JA.md`（日）；改其一时注意是否需要同步另外两份。
