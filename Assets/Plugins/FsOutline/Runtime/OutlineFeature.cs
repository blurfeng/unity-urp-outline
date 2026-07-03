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

            private readonly OutlineSettings _defaultSettings;
            private readonly Material _outlineMaterial;
            private FilteringSettings _filteringSettings;
            private readonly MaterialPropertyBlock _propertyBlock;
            private RTHandle _outlineMaskRT;

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
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                // 使用当前相机的渲染目标描述符来配置RT。
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                // 不需要太高的抗锯齿，因为只是需要外描边目标的遮罩。
                desc.msaaSamples = 1;
                // 不需要深度缓冲。
                desc.depthBufferBits = 0;
                // 选择有Alpha通道的颜色格式，后续处理需要。
                desc.colorFormat = RenderTextureFormat.ARGB32;
                // 分配 Mask RT。
                RenderingUtils.ReAllocateIfNeeded(ref _outlineMaskRT, desc, name:"_OutlineMaskRT");
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 更新设置。
                UpdateSettings();

                // 创建命令缓冲区。
                var cmd = CommandBufferPool.Get("Outline");

                // ---- 遮罩 RT ---- //
                // 设置绘制目标为_outlineMaskRT。并在渲染前清空RT。
                cmd.SetRenderTarget(_outlineMaskRT);
                cmd.ClearRenderTarget(true, true, Color.clear);

                // 设置绘制属性并添加。
                var drawingSettings = CreateDrawingSettings(_shaderTagIds, ref  renderingData, SortingCriteria.None);
                var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings);
                var list = context.CreateRendererList(ref rendererListParams);
                // 绘制一批已有的渲染器。这里的来源是 context（场景中的 Mesh Renderer），但我们指定了过滤，只绘制我们想要的物体。
                cmd.DrawRendererList(list);

                // ---- 外描边 ---- //
                // 设置绘制目标为当前相机的渲染目标。
                cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
                // 设置外描边材质属性块，传入 Mask RT。
                _propertyBlock.SetTexture(_outlineMaskId, _outlineMaskRT);
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

                // 设置外描边材质属性。
                _outlineMaterial.SetColor(_outlineColorId, color);
                _outlineMaterial.SetFloat(_outlineWidthId, width);
                _outlineMaterial.SetFloat(_outlineOpacityId, opacity);
                _outlineMaterial.SetFloat(_outlineHardnessId, hardness);
                _outlineMaterial.SetFloat(_outlinePenetrationId, penetration);
            }
        }

        [SerializeField] private Shader shader;

        // 描边 Pass 的注入时机。默认在后处理前绘制。属于 Feature 级设置，不随 Volume 运行时切换。
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        [SerializeField] private OutlineSettings settings;

        private OutlinePass _outlinePass;
        private Material _outlineMaterial;

        /// <inheritdoc/>
        public override void Create()
        {
            // Create 可能在未调用 Dispose 的情况下被重复调用（例如在 Inspector 中修改设置触发 OnValidate）。
            // 先释放上一次创建的资源，避免 Material 与 RTHandle 泄漏。
            _outlinePass?.Dispose();
            _outlinePass = null;
            CoreUtils.Destroy(_outlineMaterial);
            _outlineMaterial = null;

            // 检查 Shader 是否可用。
            if (!shader || !shader.isSupported)
            {
                Debug.LogWarning("OutlineFeature: Missing or unsupported Outline Shader.");
                return;
            }

            // 使用 shader 创建材质，并创建 Pass。
            _outlineMaterial = new Material(shader);
            _outlinePass = new OutlinePass(_outlineMaterial, settings, renderPassEvent);
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_outlinePass == null)
            {
                // Debug.LogWarning($"OutlineFeature: Missing Outline Pass. {GetType().Name} render pass will not execute.");
                return;
            }

            // 跳过材质预览缩略图与反射探针相机，避免无谓的 RT 分配与预览杂边。
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

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
