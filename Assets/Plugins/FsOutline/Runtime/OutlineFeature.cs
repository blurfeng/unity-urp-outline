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
            private static readonly int _outlinePenetrationId = Shader.PropertyToID("_OutlinePenetration");
            private static readonly int _outlineOcclusionId = Shader.PropertyToID("_OutlineOcclusion");
            private static readonly int _outlineMaskDepthId = Shader.PropertyToID("_OutlineMaskDepth");

            private readonly OutlineSettings _defaultSettings;
            private readonly Material _outlineMaterial;
            private FilteringSettings _filteringSettings;
            private readonly MaterialPropertyBlock _propertyBlock;
            private RTHandle _outlineMaskRT;
            // 遮罩的目标深度缓冲：只含被描边物体自身深度（由物体材质 ZWrite 写入），可被解析 Shader 采样。
            // 供遮挡剔除（与场景深度比较）使用。
            private RTHandle _outlineMaskDepthRT;

            // 由 ResolveOcclusion / UpdateSettings 解析出的当前生效遮挡剔除开关。开启时需要为遮罩
            // 附带一张可采样的目标深度缓冲（OnCameraSetup 分配、Execute 绑定）。
            private bool _occlusionCulling;

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
                        renderingLayerMask: (uint)_defaultSettings.renderingLayerMask);
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
            }

            /// <summary>
            /// 解析当前生效的遮挡剔除开关（Volume 覆盖优先，否则回退 Feature 默认）。
            /// 需要在 AddRenderPasses（决定是否请求深度图）与 OnCameraSetup（决定 RT 是否带深度）中提前得知。
            /// </summary>
            public bool ResolveOcclusion()
            {
                var volumeComponent = VolumeManager.instance.stack.GetComponent<Outline>();
                bool isActive = volumeComponent != null && volumeComponent.isActive.value;

                return isActive && volumeComponent.occlusionCulling.overrideState ?
                    volumeComponent.occlusionCulling.value : _defaultSettings.occlusionCulling;
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
                // 遮挡剔除时，把目标深度缓冲一并绑定：物体用自身材质绘制遮罩时会 ZWrite 写入其真实深度，
                // 遮罩覆盖度保持完整（不做遮挡切割），遮挡判定留到解析阶段处理。
                if (_occlusionCulling)
                    CoreUtils.SetRenderTarget(cmd, _outlineMaskRT, _outlineMaskDepthRT, ClearFlag.All, Color.clear);
                else
                {
                    cmd.SetRenderTarget(_outlineMaskRT);
                    cmd.ClearRenderTarget(true, true, Color.clear);
                }

                // 设置绘制属性并添加。
                var drawingSettings = CreateDrawingSettings(_shaderTagIds, ref  renderingData, SortingCriteria.None);
                var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings);
                var list = context.CreateRendererList(ref rendererListParams);
                // 绘制一批已有的渲染器。这里的来源是 context（场景中的 Mesh Renderer），但我们指定了过滤，只绘制我们想要的物体。
                cmd.DrawRendererList(list);

                // ---- 外描边 ---- //
                // 设置绘制目标为当前相机的渲染目标。
                cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
                // 设置外描边材质属性块，传入 Mask RT（及遮挡剔除时的目标深度）。
                _propertyBlock.SetTexture(_outlineMaskId, _outlineMaskRT);
                if (_occlusionCulling)
                    _propertyBlock.SetTexture(_outlineMaskDepthId, _outlineMaskDepthRT);
                // 绘制一个全屏三角形，使用外描边材质，并传入属性块。
                cmd.DrawProcedural(Matrix4x4.identity, _outlineMaterial, 0, MeshTopology.Triangles, 3, 1, _propertyBlock);

                // ---- 执行绘制 ---- //
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
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
                bool isActive = volumeComponent != null && volumeComponent.isActive.value;

                Color color = isActive && volumeComponent.color.overrideState ?
                    volumeComponent.color.value : _defaultSettings.color;

                float width = isActive && volumeComponent.width.overrideState ?
                    volumeComponent.width.value : _defaultSettings.width;

                float opacity = isActive && volumeComponent.opacity.overrideState ?
                    volumeComponent.opacity.value : _defaultSettings.opacity;

                float hardness = isActive && volumeComponent.hardness.overrideState ?
                    volumeComponent.hardness.value : _defaultSettings.hardness;

                float penetration = isActive && volumeComponent.penetration.overrideState ?
                    volumeComponent.penetration.value : _defaultSettings.penetration;

                uint renderingLayerMask = isActive && volumeComponent.renderingLayerMask.overrideState ?
                    volumeComponent.renderingLayerMask.value : (uint)_defaultSettings.renderingLayerMask;
                // 更新过滤设置，最终应用于渲染。
                _filteringSettings.renderingLayerMask = renderingLayerMask;

                // 遮挡剔除开关同步到字段，供 Execute 决定是否绑定目标深度缓冲（深度 RT 已在 OnCameraSetup 依此分配）。
                _occlusionCulling = isActive && volumeComponent.occlusionCulling.overrideState ?
                    volumeComponent.occlusionCulling.value : _defaultSettings.occlusionCulling;

                // 设置外描边材质属性。
                _outlineMaterial.SetColor(_outlineColorId, color);
                _outlineMaterial.SetFloat(_outlineWidthId, width);
                _outlineMaterial.SetFloat(_outlineOpacityId, opacity);
                _outlineMaterial.SetFloat(_outlineHardnessId, hardness);
                _outlineMaterial.SetFloat(_outlinePenetrationId, penetration);
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
