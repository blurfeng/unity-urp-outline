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
        [ColorUsage(true, true)]
        public Color outlineColor = Color.white;

        [Range(0.001f, 0.01f)] public float outlineWidth = 0.002f;

        // 默认第 1 位（通常对应名为 "Outline" 的渲染层）。层名由 RenderingLayerMaskDrawer 从 URP 设置自动读取。
        // 2022.3：RenderingLayerMask 即本命名空间下的兼容垫片 Fs.Outline.RenderingLayerMask；
        // Unity 6：经上方 using 别名指向引擎内置的 UnityEngine.RenderingLayerMask。
        public RenderingLayerMask outlineRenderingLayerMask = 2u;
    }
}
