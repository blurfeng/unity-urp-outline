// https://docs.unity.cn/cn/Packages-cn/com.unity.render-pipelines.universal@14.1/manual/renderer-features/create-custom-renderer-feature.html

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
        private static readonly int _shaderPropOutlineMask = Shader.PropertyToID("_OutlineMask");
        
        private readonly Material _outlineMaterial;
        private readonly FilteringSettings _filteringSettings;
        private readonly MaterialPropertyBlock _propertyBlock;
        private RTHandle _outlineMaskRT;
        
        public OutlinePass(Material outlineMaterial)
        {
            // Configures where the render pass should be injected.
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            
            _outlineMaterial = outlineMaterial;
            
            // 只渲染指定RenderingLayer的物体。
            // TODO: 目前硬代码Outline Layer为2，后续可以通过Feature的参数来设置。
            _filteringSettings = new FilteringSettings(RenderQueueRange.all, renderingLayerMask: 2);
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
            _propertyBlock.SetTexture(_shaderPropOutlineMask, _outlineMaskRT);
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
    }

    [SerializeField]
    private Material outlineMaterial;
    private OutlinePass _outlinePass;
    
    /// <summary>
    /// 确认材质和Shader是否有效。
    /// </summary>
    private bool IsMaterialValid => outlineMaterial && outlineMaterial.shader && outlineMaterial.shader.isSupported;

    /// <inheritdoc/>
    public override void Create()
    {
        if (!IsMaterialValid)
        {
            Debug.LogWarningFormat("Missing Outline Material. {0} render pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
            return;
        }
        
        _outlinePass = new OutlinePass(outlineMaterial);
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_outlinePass == null)
        {
            Debug.LogWarningFormat("Missing Outline Pass. {0} render pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
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


