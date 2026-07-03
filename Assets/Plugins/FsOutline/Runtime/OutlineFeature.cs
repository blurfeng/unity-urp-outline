// https://docs.unity.cn/cn/Packages-cn/com.unity.render-pipelines.universal@14.1/manual/renderer-features/create-custom-renderer-feature.html

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Volumes;

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
        
        private readonly OutlineSettings _defaultSettings;
        private readonly Material _outlineMaterial;
        private FilteringSettings _filteringSettings;
        private readonly MaterialPropertyBlock _propertyBlock;
        private RTHandle _outlineMaskRT;
        
        public OutlinePass(Material outlineMaterial, OutlineSettings defaultSettings)
        {
            // Configures where the render pass should be injected.
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            
            _outlineMaterial = outlineMaterial;
            _defaultSettings = defaultSettings;
            
            _propertyBlock = new MaterialPropertyBlock();
            
            // 只渲染指定 Rendering Layer 的物体。
            _filteringSettings = 
                new FilteringSettings(
                    RenderQueueRange.all, 
                    renderingLayerMask: (uint)_defaultSettings.outlineRenderingLayerMask);
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

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Debug.Log("OutlineRenderPass OnCameraCleanup");
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
            
            Color outlineColor = isActive && volumeComponent.outlineColor.overrideState ?
                volumeComponent.outlineColor.value : _defaultSettings.outlineColor;
            
            float outlineWidth = isActive && volumeComponent.outlineWidth.overrideState ?
                volumeComponent.outlineWidth.value : _defaultSettings.outlineWidth;
            
            uint outlineRenderingLayerMask = isActive && volumeComponent.outlineRenderingLayerMask.overrideState ?
                volumeComponent.outlineRenderingLayerMask.value : (uint)_defaultSettings.outlineRenderingLayerMask;
            // 更新过滤设置，最终应用于渲染。
            if (outlineRenderingLayerMask != _filteringSettings.renderingLayerMask)
            {
                _filteringSettings.renderingLayerMask = outlineRenderingLayerMask;
            }
            
            // 设置外描边材质属性。
            _outlineMaterial.SetColor(_outlineColorId, outlineColor);
            _outlineMaterial.SetFloat(_outlineWidthId, outlineWidth);
        }
    }

    [SerializeField] private Shader shader;
    [SerializeField] private OutlineSettings settings;
    
    private OutlinePass _outlinePass;
    private Material _outlineMaterial;
    
    /// <inheritdoc/>
    public override void Create()
    {
        // 检查 Shader 是否可用。
        if (!shader || !shader.isSupported)
        {
            Debug.LogWarning("OutlineFeature: Missing or unsupported Outline Shader.");
            return;
        }
        
        // 使用 shader 创建材质，并创建 Pass。
        _outlineMaterial = new Material(shader);
        _outlinePass = new OutlinePass(_outlineMaterial, settings);
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
        
        // 释放 Pass 相关资源。
        _outlinePass?.Dispose();
    }
}