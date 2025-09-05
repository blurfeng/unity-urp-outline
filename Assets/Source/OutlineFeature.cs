// https://docs.unity.cn/cn/Packages-cn/com.unity.render-pipelines.universal@14.1/manual/renderer-features/create-custom-renderer-feature.html

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        
        private OutlineSettings _defaultSettings;
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
            
            // 只渲染指定RenderingLayer的物体。
            // TODO: 目前硬代码Outline Layer为2，后续可以通过Feature的参数来设置。
            _filteringSettings = new FilteringSettings(RenderQueueRange.all, renderingLayerMask: (uint)_defaultSettings.outlineRenderingLayerMask);
            _propertyBlock = new MaterialPropertyBlock();
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
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            // 不需要太高的抗锯齿，因为只是需要外描边目标的遮罩。
            desc.msaaSamples = 1;
            // 不需要深度缓冲。
            desc.depthBufferBits = 0;
            // 保留Alpha通道，以防后续处理需要。
            desc.colorFormat = RenderTextureFormat.ARGB32;
            RenderingUtils.ReAllocateIfNeeded(ref _outlineMaskRT, desc, name:"_OutlineMaskRT");
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            UpdateSettings();
            
            var cmd = CommandBufferPool.Get("Outline Command");
            
            // ---- 绘制目标 RT ----//
            // 设置绘制目标为_outlineMaskRT。并在渲染前清空RT。
            cmd.SetRenderTarget(_outlineMaskRT);
            cmd.ClearRenderTarget(true, true, Color.clear);
            var drawingSettings = CreateDrawingSettings(_shaderTagIds, ref  renderingData, SortingCriteria.None);
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings);
            var list = context.CreateRendererList(ref rendererListParams);
            cmd.DrawRendererList(list);
            
            // ---- 绘制外描边 ----//
            // 设置绘制目标为当前相机的渲染目标。
            cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            _propertyBlock.SetTexture(_outlineMaskId, _outlineMaskRT);
            cmd.DrawProcedural(Matrix4x4.identity, _outlineMaterial, 0, MeshTopology.Triangles, 3, 1, _propertyBlock);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Debug.Log("OutlineRenderPass OnCameraCleanup");
        }
        
        private void UpdateSettings()
        {
            if (_outlineMaterial == null) return;

            // 获取 Volume 设置或使用默认值
            var volumeComponent = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();
            bool isActive = volumeComponent != null && volumeComponent.isActive.value;
            
            Color outlineColor = isActive && volumeComponent.outlineColor.overrideState ?
                volumeComponent.outlineColor.value : _defaultSettings.outlineColor;
            float outlineWidth = isActive && volumeComponent.outlineWidth.overrideState ?
                volumeComponent.outlineWidth.value : _defaultSettings.outlineWidth;
            uint outlineRenderingLayerMask = isActive && volumeComponent.outlineRenderingLayerMask.overrideState ?
                volumeComponent.outlineRenderingLayerMask.value : (uint)_defaultSettings.outlineRenderingLayerMask;

            if (outlineRenderingLayerMask != _filteringSettings.renderingLayerMask)
            {
                // 更新过滤设置
                _filteringSettings.renderingLayerMask = outlineRenderingLayerMask;
            }
            
            _outlineMaterial.SetColor(_outlineColorId, outlineColor);
            _outlineMaterial.SetFloat(_outlineWidthId, outlineWidth);
            
        }
    }

    [SerializeField] private Shader shader;
    [SerializeField] private OutlineSettings settings;
    private Material _outlineMaterial;
    private OutlinePass _outlinePass;
    
    /// <inheritdoc/>
    public override void Create()
    {
        if (!shader || !shader.isSupported)
        {
            Debug.LogWarning("OutlineFeature: Missing or unsupported Outline Shader.");
            return;
        }
        
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
        
        renderer.EnqueuePass(_outlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        // 释放Pass中的资源。
        _outlinePass?.Dispose();
    }
}

[Serializable]
public class OutlineSettings
{
    [ColorUsage(true, true)]
    public Color outlineColor = Color.white;
    [Range(0.001f, 0.01f)] public float outlineWidth = 0.002f;
    public ERenderingLayerMask outlineRenderingLayerMask = ERenderingLayerMask.Outline;
}