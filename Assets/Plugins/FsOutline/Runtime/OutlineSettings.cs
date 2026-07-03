using System;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using RenderingLayerMask = UnityEngine.RenderingLayerMask;
#else
using RenderingLayerMask = Fs.Outline.RenderingLayerMask;
#endif

[Serializable]
public class OutlineSettings
{
    [ColorUsage(true, true)]
    public Color outlineColor = Color.white;

    [Range(0.001f, 0.01f)] public float outlineWidth = 0.002f;

    // 默认第 1 位（通常对应名为 "Outline" 的渲染层）。层名由 RenderingLayerMaskDrawer 从 URP 设置自动读取。
    public RenderingLayerMask outlineRenderingLayerMask = 2u;
}
