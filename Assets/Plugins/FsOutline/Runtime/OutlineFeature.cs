// https://docs.unity.cn/cn/Packages-cn/com.unity.render-pipelines.universal@14.1/manual/renderer-features/create-custom-renderer-feature.html

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fs.Outline
{
    public class OutlineFeature : ScriptableRendererFeature
    {
        class OutlinePass : ScriptableRenderPass
        {
            private static readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
            };
            // Shader 属性 ID。
            private static readonly int _outlineMaskId = Shader.PropertyToID("_OutlineMask");
            private static readonly int _outlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int _outlineWidthId = Shader.PropertyToID("_OutlineWidth");
            private static readonly int _outlineOpacityId = Shader.PropertyToID("_OutlineOpacity");
            private static readonly int _outlineHardnessId = Shader.PropertyToID("_OutlineHardness");
            private static readonly int _outlineOcclusionId = Shader.PropertyToID("_OutlineOcclusion");
            private static readonly int _outlineMaskDepthId = Shader.PropertyToID("_OutlineMaskDepth");
            private static readonly int _outlineJFAId = Shader.PropertyToID("_OutlineJFA");
            private static readonly int _jfaStepId = Shader.PropertyToID("_JFAStep");
            private static readonly int _outlineBlurId = Shader.PropertyToID("_OutlineBlur");

            // Outline.shader 的 Pass 索引（与 Shader 内 Pass 顺序严格对应）。
            private const int ResolveDilatePassIndex = 0;   // 膨胀：单环 8 抽样解析
            private const int MaskCoveragePassIndex = 1;  // 透明队列覆盖度 overrideMaterial
            private const int JFASeedPassIndex = 2;       // JFA 播种
            private const int JFAJumpPassIndex = 3;        // JFA 跳跃迭代
            private const int ResolveJFAPassIndex = 4;     // JFA 距离场解析
            private const int BlurHPassIndex = 5;          // 可分离模糊：横向
            private const int BlurVPassIndex = 6;          // 可分离模糊：纵向
            private const int ResolveBlurPassIndex = 7;    // 模糊覆盖度解析

            private readonly OutlineSettings _defaultSettings;
            private readonly Material _outlineMaterial;
            private FilteringSettings _filteringSettings;
            private readonly MaterialPropertyBlock _propertyBlock;
            private RTHandle _outlineMaskRT;
            // 遮罩的目标深度缓冲：只含被描边物体自身深度（由物体材质 ZWrite 写入），可被解析 Shader 采样。
            // 供遮挡剔除（与场景深度比较）使用。
            private RTHandle _outlineMaskDepthRT;
            // JFA 位置图（存最近种子像素坐标，RGFloat）。两张乒乓迭代。仅 JumpFlood 模式分配。
            private RTHandle _jfaRTA;
            private RTHandle _jfaRTB;
            // 可分离模糊的覆盖度临时图（R8）。A=横向结果、B=纵向结果。仅 SeparableBlur 模式分配。
            private RTHandle _blurRTA;
            private RTHandle _blurRTB;

            // 由 ResolveOcclusion / UpdateSettings 解析出的当前生效遮挡剔除开关。开启时需要为遮罩
            // 附带一张可采样的目标深度缓冲（OnCameraSetup 分配、Execute 绑定）。
            private bool _occlusionCulling;

            // 由 ResolveExpandMode / UpdateSettings 解析出的当前生效扩展模式（决定分配哪些 RT、走哪条解析路径）。
            private OutlineExpandMode _expandMode;
            // 当前生效描边宽度（UpdateSettings 解析）。Execute 需要用它换算 JFA 跳跃迭代的起始步长。
            private float _width;

            public OutlinePass(Material outlineMaterial, OutlineSettings defaultSettings, RenderPassEvent renderPassEvent)
            {
                // Configures where the render pass should be injected.
                this.renderPassEvent = renderPassEvent;

                _outlineMaterial = outlineMaterial;
                _defaultSettings = defaultSettings;

                _propertyBlock = new MaterialPropertyBlock();

                // 只渲染指定 Rendering Layer 的物体。
                _filteringSettings =
                    new FilteringSettings(
                        RenderQueueRange.all,
                        renderingLayerMask: (uint)_defaultSettings.RenderingLayerMask);
            }

            /// <summary>
            /// 释放资源。
            /// </summary>
            public void Dispose()
            {
                _outlineMaskRT?.Release();
                _outlineMaskRT = null;
                _outlineMaskDepthRT?.Release();
                _outlineMaskDepthRT = null;
                ReleaseJFARTs();
                ReleaseBlurRTs();
            }

            // 释放 JFA 位置图（切换到非 JumpFlood 模式或释放资源时调用，避免常驻显存）。
            private void ReleaseJFARTs()
            {
                _jfaRTA?.Release();
                _jfaRTA = null;
                _jfaRTB?.Release();
                _jfaRTB = null;
            }

            // 释放模糊临时图（切换到非 SeparableBlur 模式或释放资源时调用）。
            private void ReleaseBlurRTs()
            {
                _blurRTA?.Release();
                _blurRTA = null;
                _blurRTB?.Release();
                _blurRTB = null;
            }

            /// <summary>
            /// 解析当前生效的遮挡剔除开关（Volume 覆盖优先，否则回退 Feature 默认）。
            /// 需要在 AddRenderPasses（决定是否请求深度图）与 OnCameraSetup（决定 RT 是否带深度）中提前得知。
            /// </summary>
            public bool ResolveOcclusion()
            {
                var volumeComponent = VolumeManager.instance.stack.GetComponent<Outline>();
                bool isActive = volumeComponent != null && volumeComponent.IsActive.value;

                return isActive && volumeComponent.OcclusionCulling.overrideState ?
                    volumeComponent.OcclusionCulling.value : _defaultSettings.OcclusionCulling;
            }

            /// <summary>
            /// 解析当前生效的扩展模式（Volume 覆盖优先，否则回退 Feature 默认）。
            /// 需要在 OnCameraSetup 中提前得知，以决定分配哪些中间 RT。
            /// </summary>
            public OutlineExpandMode ResolveExpandMode()
            {
                var volumeComponent = VolumeManager.instance.stack.GetComponent<Outline>();
                bool isActive = volumeComponent != null && volumeComponent.IsActive.value;

                return isActive && volumeComponent.ExpandMode.overrideState ?
                    volumeComponent.ExpandMode.value : _defaultSettings.ExpandMode;
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                // 提前解析开关：遮挡剔除需要一张可采样的目标深度缓冲。
                _occlusionCulling = ResolveOcclusion();
                // 提前解析扩展模式：决定分配哪些中间 RT。
                _expandMode = ResolveExpandMode();

                // 使用当前相机的渲染目标描述符来配置RT。
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                // 不需要太高的抗锯齿，因为只是需要外描边目标的遮罩。
                desc.msaaSamples = 1;
                // 颜色格式带 Alpha 记录覆盖度；深度单独分配到 _outlineMaskDepthRT，故此处不带深度。
                desc.depthBufferBits = 0;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                // 分配 Mask RT。
                RenderingUtils.ReAllocateIfNeeded(ref _outlineMaskRT, desc, name:"_OutlineMaskRT");

                // 目标深度缓冲：分配为可采样的深度纹理。物体用自身材质绘制遮罩时 ZWrite 写入其真实深度，
                // 解析阶段据此做遮挡剔除（与场景深度比较）。仅在遮挡剔除开启时分配。
                if (_occlusionCulling)
                {
                    RenderTextureDescriptor depthDesc = renderingData.cameraData.cameraTargetDescriptor;
                    depthDesc.msaaSamples = 1;
                    // 可采样的深度纹理（与 URP 生成 _CameraDepthTexture 同一套路），32 位保证远处精度。
                    depthDesc.colorFormat = RenderTextureFormat.Depth;
                    depthDesc.depthBufferBits = 32;
                    RenderingUtils.ReAllocateIfNeeded(
                        ref _outlineMaskDepthRT, depthDesc, FilterMode.Point, TextureWrapMode.Clamp,
                        name: "_OutlineMaskDepthRT");
                }

                // 按扩展模式分配中间 RT，并释放非当前模式的 RT，避免常驻显存浪费。
                if (_expandMode == OutlineExpandMode.JumpFlood)
                {
                    RenderTextureDescriptor jfaDesc = renderingData.cameraData.cameraTargetDescriptor;
                    jfaDesc.msaaSamples = 1;
                    jfaDesc.depthBufferBits = 0;
                    // 存最近种子的像素坐标；用浮点保证大分辨率下坐标精度（RGHalf 在 2K+ 会丢精度）。
                    jfaDesc.colorFormat = RenderTextureFormat.RGFloat;
                    RenderingUtils.ReAllocateIfNeeded(
                        ref _jfaRTA, jfaDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_OutlineJFA_A");
                    RenderingUtils.ReAllocateIfNeeded(
                        ref _jfaRTB, jfaDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_OutlineJFA_B");
                    ReleaseBlurRTs();
                }
                else if (_expandMode == OutlineExpandMode.SeparableBlur)
                {
                    RenderTextureDescriptor blurDesc = renderingData.cameraData.cameraTargetDescriptor;
                    blurDesc.msaaSamples = 1;
                    blurDesc.depthBufferBits = 0;
                    // 单通道覆盖度场；线性过滤便于解析阶段平滑采样。
                    blurDesc.colorFormat = RenderTextureFormat.R8;
                    RenderingUtils.ReAllocateIfNeeded(
                        ref _blurRTA, blurDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_OutlineBlur_A");
                    RenderingUtils.ReAllocateIfNeeded(
                        ref _blurRTB, blurDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_OutlineBlur_B");
                    ReleaseJFARTs();
                }
                else
                {
                    // Dilate：不需要中间 RT。
                    ReleaseJFARTs();
                    ReleaseBlurRTs();
                }
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 材质可能在本 Pass 入队后、执行前被 Create()/Dispose() 销毁（首次打开场景时资源重导入常触发）。
                // 材质缺失时直接跳过本 Pass，避免向 DrawProcedural 传入 null 材质而抛 ArgumentNullException。
                if (_outlineMaterial == null) return;

                // 更新设置。
                UpdateSettings();

                // 创建命令缓冲区。
                var cmd = CommandBufferPool.Get("Outline");

                // ---- 遮罩 RT ---- //
                // 遮挡剔除时，把目标深度缓冲一并绑定：物体绘制遮罩时会 ZWrite 写入其真实深度，
                // 遮罩覆盖度保持完整（不做遮挡切割），遮挡判定留到解析阶段处理。
                if (_occlusionCulling)
                    CoreUtils.SetRenderTarget(cmd, _outlineMaskRT, _outlineMaskDepthRT, ClearFlag.All, Color.clear);
                else
                {
                    cmd.SetRenderTarget(_outlineMaskRT);
                    cmd.ClearRenderTarget(true, true, Color.clear);
                }
                // 先把渲染目标绑定与清屏 flush 出去，使其对随后的 context.DrawRenderers 生效。
                // DrawRenderers 直接绘制到“当前已绑定的 RT”，若不先执行本 cmd，绘制会发生在绑定之前。
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // ---- 绘制遮罩 ---- //
                // 遮罩需要“几何覆盖度”：物体所在处 alpha=1、否则 0，与材质自身透明度无关。按渲染队列拆分绘制：
                //  · 不透明 / cutout 队列：用物体自身材质绘制，保留 alpha 裁剪，且不透明输出 alpha≈1，覆盖度天然正确；
                //    遮挡剔除开启时其 ZWrite 也会把真实深度写入 _OutlineMaskDepth。
                //  · 透明队列：改用 overrideMaterial（本 Shader 的覆盖度 Pass）强制输出 alpha=1 并 ZWrite；
                //    否则透明材质混合后的低 alpha 会被当成“几乎没有覆盖”，导致透明物体描不出边、也无法参与遮挡判定。
                // 用 context.DrawRenderers（而非 RendererList）以便对透明队列指定 overrideMaterial。
                var drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, SortingCriteria.None);

                // 不透明队列：物体自身材质。
                var opaqueFiltering = _filteringSettings;
                opaqueFiltering.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref opaqueFiltering);

                // 透明队列：覆盖度替换材质（本材质的覆盖度 Pass，强制 alpha=1 + ZWrite）。
                var transparentDrawing = drawingSettings;
                transparentDrawing.overrideMaterial = _outlineMaterial;
                transparentDrawing.overrideMaterialPassIndex = MaskCoveragePassIndex;
                var transparentFiltering = _filteringSettings;
                transparentFiltering.renderQueueRange = RenderQueueRange.transparent;
                context.DrawRenderers(renderingData.cullResults, ref transparentDrawing, ref transparentFiltering);

                // ---- 外描边 ---- //
                // 传入 Mask RT（及遮挡剔除时的目标深度），随后按扩展模式选择解析路径。
                _propertyBlock.SetTexture(_outlineMaskId, _outlineMaskRT);
                if (_occlusionCulling)
                    _propertyBlock.SetTexture(_outlineMaskDepthId, _outlineMaskDepthRT);

                var cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                int screenWidth = renderingData.cameraData.cameraTargetDescriptor.width;

                switch (_expandMode)
                {
                    case OutlineExpandMode.JumpFlood:
                        ExecuteJumpFlood(cmd, cameraColor, screenWidth);
                        break;
                    case OutlineExpandMode.SeparableBlur:
                        ExecuteSeparableBlur(cmd, cameraColor);
                        break;
                    case OutlineExpandMode.Dilate:
                    default: // Dilate：单环 8 抽样，直接解析到相机颜色。
                        DrawFullscreen(cmd, cameraColor, ResolveDilatePassIndex);
                        break;
                }

                // ---- 执行绘制 ---- //
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            /// <summary>
            /// 用外描边材质的指定 Pass 向 target 绘制一个全屏三角形（附带当前属性块）。
            /// </summary>
            private void DrawFullscreen(CommandBuffer cmd, RTHandle target, int passIndex)
            {
                cmd.SetRenderTarget(target);
                cmd.DrawProcedural(Matrix4x4.identity, _outlineMaterial, passIndex, MeshTopology.Triangles, 3, 1, _propertyBlock);
            }

            /// <summary>
            /// JumpFlood 距离场：播种 → 若干次跳跃迭代（步长减半、两张位置图乒乓）→ 距离场解析。
            /// </summary>
            private void ExecuteJumpFlood(CommandBuffer cmd, RTHandle cameraColor, int screenWidth)
            {
                // RT 未就绪（模式在 OnCameraSetup 与 Execute 之间异常不一致）时退回 Dilate，避免向空 RT 绘制。
                if (_jfaRTA == null || _jfaRTB == null)
                {
                    DrawFullscreen(cmd, cameraColor, ResolveDilatePassIndex);
                    return;
                }

                // 播种：内部像素写自身像素坐标（属性块已含 _OutlineMask）。
                DrawFullscreen(cmd, _jfaRTA, JFASeedPassIndex);

                // 跳跃步长：从 ≥描边像素宽度的最小 2 次幂起，逐次减半至 1。仅覆盖描边宽度邻域，无需全屏泛洪。
                int maxStep = Mathf.Max(1, Mathf.CeilToInt(_width * screenWidth));
                int step = 1;
                while (step < maxStep) step <<= 1;

                RTHandle src = _jfaRTA, dst = _jfaRTB;
                for (; step >= 1; step >>= 1)
                {
                    _propertyBlock.SetTexture(_outlineJFAId, src);
                    _propertyBlock.SetFloat(_jfaStepId, step);
                    DrawFullscreen(cmd, dst, JFAJumpPassIndex);
                    (src, dst) = (dst, src);
                }

                // 解析：迭代结束后最终结果在 src。
                _propertyBlock.SetTexture(_outlineJFAId, src);
                DrawFullscreen(cmd, cameraColor, ResolveJFAPassIndex);
            }

            /// <summary>
            /// 可分离模糊：横向模糊 → 纵向模糊 → 覆盖度解析。
            /// </summary>
            private void ExecuteSeparableBlur(CommandBuffer cmd, RTHandle cameraColor)
            {
                if (_blurRTA == null || _blurRTB == null)
                {
                    DrawFullscreen(cmd, cameraColor, ResolveDilatePassIndex);
                    return;
                }

                // 横向模糊（读 _OutlineMask）→ A。
                DrawFullscreen(cmd, _blurRTA, BlurHPassIndex);
                // 纵向模糊（读 A）→ B。
                _propertyBlock.SetTexture(_outlineBlurId, _blurRTA);
                DrawFullscreen(cmd, _blurRTB, BlurVPassIndex);
                // 解析（读 B）。
                _propertyBlock.SetTexture(_outlineBlurId, _blurRTB);
                DrawFullscreen(cmd, cameraColor, ResolveBlurPassIndex);
            }

            /// <summary>
            /// 更新设置。
            /// 每次执行时都会调用来支持运行时动态修改配置，确保设置是最新的。
            /// </summary>
            private void UpdateSettings()
            {
                if (_outlineMaterial == null) return;

                // 获取 Volume 设置或使用默认值。
                var volumeComponent = VolumeManager.instance.stack.GetComponent<Outline>();
                bool isActive = volumeComponent != null && volumeComponent.IsActive.value;

                Color color = isActive && volumeComponent.Color.overrideState ?
                    volumeComponent.Color.value : _defaultSettings.Color;

                float width = isActive && volumeComponent.Width.overrideState ?
                    volumeComponent.Width.value : _defaultSettings.Width;

                float opacity = isActive && volumeComponent.Opacity.overrideState ?
                    volumeComponent.Opacity.value : _defaultSettings.Opacity;

                float hardness = isActive && volumeComponent.Hardness.overrideState ?
                    volumeComponent.Hardness.value : _defaultSettings.Hardness;

                uint renderingLayerMask = isActive && volumeComponent.RenderingLayerMask.overrideState ?
                    volumeComponent.RenderingLayerMask.value : (uint)_defaultSettings.RenderingLayerMask;
                // 更新过滤设置，最终应用于渲染。
                _filteringSettings.renderingLayerMask = renderingLayerMask;

                // 遮挡剔除开关同步到字段，供 Execute 决定是否绑定目标深度缓冲（深度 RT 已在 OnCameraSetup 依此分配）。
                _occlusionCulling = isActive && volumeComponent.OcclusionCulling.overrideState ?
                    volumeComponent.OcclusionCulling.value : _defaultSettings.OcclusionCulling;

                // 扩展模式同步到字段，供 Execute 选择解析路径（中间 RT 已在 OnCameraSetup 依此分配）。
                _expandMode = isActive && volumeComponent.ExpandMode.overrideState ?
                    volumeComponent.ExpandMode.value : _defaultSettings.ExpandMode;

                // 缓存宽度供 Execute 换算 JFA 跳跃迭代起始步长。
                _width = width;

                // 设置外描边材质属性。
                _outlineMaterial.SetColor(_outlineColorId, color);
                _outlineMaterial.SetFloat(_outlineWidthId, width);
                _outlineMaterial.SetFloat(_outlineOpacityId, opacity);
                _outlineMaterial.SetFloat(_outlineHardnessId, hardness);
                _outlineMaterial.SetFloat(_outlineOcclusionId, _occlusionCulling ? 1f : 0f);
            }
        }

        [Tooltip("描边使用的 Shader，通常保持默认（内置 Outline.shader）。")]
        [SerializeField] private Shader shader;

        // 描边 Pass 的注入时机。默认在后处理前绘制。属于 Feature 级设置，不随 Volume 运行时切换。
        [Tooltip("描边在渲染管线中的注入时机，属 Renderer Feature 级设置（不随 Volume 运行时切换），默认在后处理前绘制。")]
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        [Tooltip("描边的默认设置。可被场景中的 Volume（Outline 组件）在运行时覆盖。")]
        [SerializeField] private OutlineSettings settings;

        private OutlinePass _outlinePass;
        private Material _outlineMaterial;
        // 避免 Shader 缺失时每帧重复输出警告。
        private bool _shaderWarningLogged;

        /// <inheritdoc/>
        public override void Create()
        {
            // Create 可能在未调用 Dispose 的情况下被重复调用（例如在 Inspector 中修改设置触发 OnValidate）。
            // 先释放上一次创建的资源，避免 Material 与 RTHandle 泄漏。
            _outlinePass?.Dispose();
            _outlinePass = null;
            CoreUtils.Destroy(_outlineMaterial);
            _outlineMaterial = null;

            // 优先使用序列化引用的 Shader；首次导入项目时该引用可能因资源导入时序而暂为空，
            // 此时从 Resources 兜底加载（Outline.shader 位于 Resources 目录）。
            Shader outlineShader = shader != null ? shader : Resources.Load<Shader>("Outline");

            // 检查 Shader 是否可用。
            if (!outlineShader || !outlineShader.isSupported)
            {
                if (!_shaderWarningLogged)
                {
                    Debug.LogWarning("OutlineFeature: Missing or unsupported Outline Shader.");
                    _shaderWarningLogged = true;
                }
                return;
            }
            _shaderWarningLogged = false;

            // 使用 shader 创建材质，并创建 Pass。
            _outlineMaterial = new Material(outlineShader);
            _outlinePass = new OutlinePass(_outlineMaterial, settings, renderPassEvent);
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 惰性补建资源。首次打开场景时 Create() 可能早于 Shader 导入完成而未能创建材质，
            // 表现为“必须先运行一次才出现描边”。此处在真正渲染前再尝试创建一次，确保进入场景第一帧即可显示。
            if (_outlinePass == null || _outlineMaterial == null)
                Create();

            if (_outlinePass == null || _outlineMaterial == null)
            {
                // Debug.LogWarning($"OutlineFeature: Missing Outline Pass. {GetType().Name} render pass will not execute.");
                return;
            }

            // 跳过材质预览缩略图与反射探针相机，避免无谓的 RT 分配与预览杂边。
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            // 遮挡剔除需要相机场景深度（与目标自身深度比较）。仅在其开启时请求，避免无谓地强制生成深度纹理。
            _outlinePass.ConfigureInput(_outlinePass.ResolveOcclusion() ?
                ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);

            // 将 Pass 注入渲染器队列。
            renderer.EnqueuePass(_outlinePass);
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // 释放 Pass 与材质资源。
            _outlinePass?.Dispose();
            _outlinePass = null;
            CoreUtils.Destroy(_outlineMaterial);
            _outlineMaterial = null;
        }
    }
}
