using System;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

namespace Fs.Outline
{
    /// <summary>
    /// 外描边扩展算法的 Volume 参数（Inspector 自动生成枚举下拉）。
    /// </summary>
    [Serializable]
    public sealed class OutlineExpandModeParameter : VolumeParameter<OutlineExpandMode>
    {
        public OutlineExpandModeParameter(OutlineExpandMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable]
    public class Outline : VolumeComponent
    {
        [Tooltip("是否启用本 Volume 的描边覆盖。关闭时回退到 Renderer Feature 的默认设置。")]
        public BoolParameter IsActive = new BoolParameter(true, true);

        [Tooltip("外描边扩展算法。Dilate：轻量 8 抽样膨胀，大宽度会八边形穿帮；JumpFlood：真实距离场，任意宽度都平滑等宽（推荐）；SeparableBlur：辉光式软边；")]
        public OutlineExpandModeParameter ExpandMode = new OutlineExpandModeParameter(OutlineExpandMode.JumpFlood);

        [Tooltip("描边使用的高动态范围（HDR）颜色，可呈现发光效果。")]
        public ColorParameter Color = new ColorParameter(new Color(4f,4f,2f, 1f), true, true, true);

        [Tooltip("描边宽度，基于 UV 采样实现。宽度过大时可能在直角或规则形状处出现穿帮。")]
        public ClampedFloatParameter Width = new ClampedFloatParameter(0.002f, 0.001f, 0.05f);

        [Tooltip("描边整体不透明度（0–1）。")]
        public ClampedFloatParameter Opacity = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("边缘由柔和到锐利，作为幂次整形（越大越锐越细、越小越柔越粗）。默认 0.1 复现均匀实心的外描边带。")]
        public ClampedFloatParameter Hardness = new ClampedFloatParameter(0.01f, 0.01f, 4f);

        [Tooltip("遮挡剔除：开启后，被其他物体遮挡的部分不绘制描边（需要相机深度图，会自动请求）。")]
        public BoolParameter OcclusionCulling = new BoolParameter(false);

        [HideInInspector]
        public UIntParameter RenderingLayerMask = new UIntParameter(2);
    }
}
