using System;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

namespace Fs.Outline
{
    [Serializable]
    public class Outline : VolumeComponent
    {
        [Tooltip("是否启用本 Volume 的描边覆盖。关闭时回退到 Renderer Feature 的默认设置。")]
        public BoolParameter isActive = new BoolParameter(true, true);
        
        [Tooltip("描边使用的高动态范围（HDR）颜色，可呈现发光效果。")]
        public ColorParameter color = new ColorParameter(new Color(4f,4f,2f, 1f), true, true, true);

        [Tooltip("描边宽度，基于 UV 采样实现。宽度过大时可能在直角或规则形状处出现穿帮。")]
        public ClampedFloatParameter width = new ClampedFloatParameter(0.002f, 0.001f, 0.05f);

        [Tooltip("描边整体不透明度（0–1）。")]
        public ClampedFloatParameter opacity = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("边缘由柔和到锐利，作为幂次整形（越大越锐越细、越小越柔越粗）。默认 0.1 复现均匀实心的外描边带。")]
        public ClampedFloatParameter hardness = new ClampedFloatParameter(0.1f, 0.1f, 4f);

        [Tooltip("遮挡剔除：开启后，被其他物体遮挡的部分不绘制描边（需要相机深度图，会自动请求）。")]
        public BoolParameter occlusionCulling = new BoolParameter(false);

        [HideInInspector]
        public UIntParameter renderingLayerMask = new UIntParameter(2);
    }
}
