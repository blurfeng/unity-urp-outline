using System;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using RenderingLayerMask = UnityEngine.RenderingLayerMask;
#endif

namespace Fs.Outline
{
    [Serializable]
    public class OutlineSettings
    {
        [Tooltip("描边使用的高动态范围（HDR）颜色，可呈现发光效果。")]
        [ColorUsage(true, true)]
        public Color color = Color.white;

        [Tooltip("描边宽度，基于 UV 采样实现。宽度过大时可能在直角或规则形状处出现穿帮。")]
        [Range(0.001f, 0.05f)] public float width = 0.002f;

        [Tooltip("描边整体不透明度（0–1）。")]
        [Range(0f, 1f)] public float opacity = 1f;

        [Tooltip("边缘由柔和到锐利，作为幂次整形（>1 更锐利更细，<1 更柔和更粗）。")]
        [Range(0.25f, 4f)] public float hardness = 1f;

        [Tooltip("描边向物体内部渗入的深度，越大越深、越小越贴外缘。")]
        [Range(0.05f, 1f)] public float penetration = 0.5f;

        [Tooltip("遮挡剔除：开启后，被其他物体遮挡的部分不绘制描边（需要相机深度图，会自动请求）。")]
        public bool occlusionCulling = false;

        // 默认第 1 位（通常对应名为 "Outline" 的渲染层）。层名由 RenderingLayerMaskDrawer 从 URP 设置自动读取。
        // 2022.3：RenderingLayerMask 即本命名空间下的兼容垫片 Fs.Outline.RenderingLayerMask；
        // Unity 6：经上方 using 别名指向引擎内置的 UnityEngine.RenderingLayerMask。
        [Tooltip("通过渲染层（Rendering Layers）控制哪些物体会被描边。")]
        public RenderingLayerMask renderingLayerMask = 2u;
    }
}
