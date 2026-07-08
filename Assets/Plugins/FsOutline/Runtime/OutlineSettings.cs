using System;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using RenderingLayerMask = UnityEngine.RenderingLayerMask;
#endif

namespace Fs.Outline
{
    /// <summary>
    /// 外描边扩展算法。决定“如何从遮罩向外生成描边过渡带”。
    /// </summary>
    public enum OutlineExpandMode
    {
        /// <summary>
        /// 膨胀（轻量 8 抽样）：对遮罩做 8 邻域形态学膨胀。三种模式里开销最低——只有单个全屏解析
        /// Pass（9 抽样），不需要中间 RT。缺点是大宽度会八边形穿帮。
        /// </summary>
        Dilate,

        /// <summary>
        /// 跳跃泛洪 JFA（推荐）：构建真实欧氏距离场，任意宽度都平滑等宽。开销最高——播种 +
        /// 若干次跳跃迭代（迭代次数随描边像素宽度按 log2 增长）+ 解析，常见约 5~9 个全屏 Pass，
        /// 且中间要两张高精度 RGFloat 距离场贴图乒乓（带宽更高）。
        /// </summary>
        JumpFlood,

        /// <summary>
        /// 可分离模糊：横向 + 纵向高斯得到平滑覆盖度场后再解析，辉光式软边。开销居中且固定——
        /// 恒为 3 个全屏 Pass（两趟 12-tap 可分离高斯 + 解析），与描边宽度无关。
        /// </summary>
        SeparableBlur,
    }

    [Serializable]
    public class OutlineSettings
    {
        [Tooltip("外描边扩展算法。性能开销 Dilate < SeparableBlur < JumpFlood。" +
                 "JumpFlood：真实距离场，任意宽度都平滑等宽（推荐；开销最高，Pass 数随宽度增长）；" +
                 "SeparableBlur：辉光式软边（恒 3 个全屏 Pass，居中）；" +
                 "Dilate：轻量 8 抽样膨胀（最省，但大宽度会八边形穿帮）。")]
        public OutlineExpandMode ExpandMode = OutlineExpandMode.JumpFlood;

        [Tooltip("描边使用的高动态范围（HDR）颜色，可呈现发光效果。")]
        [ColorUsage(true, true)]
        public Color Color = Color.white;

        [Tooltip("描边宽度，基于 UV 采样实现。宽度过大时可能在直角或规则形状处出现穿帮。")]
        [Range(0.001f, 0.05f)] public float Width = 0.002f;

        [Tooltip("描边整体不透明度（0–1）。")]
        [Range(0f, 1f)] public float Opacity = 1f;

        [Tooltip("边缘由柔和到锐利，作为幂次整形（越大越锐越细、越小越柔越粗）。默认 0.01 复现均匀实心的外描边带。")]
        [Range(0.01f, 4f)] public float Hardness = 0.01f;

        [Tooltip("遮挡剔除：开启后，被其他物体遮挡的部分不绘制描边（需要相机深度图，会自动请求）。")]
        public bool OcclusionCulling;

        // 默认第 1 位（通常对应名为 "Outline" 的渲染层）。层名由 RenderingLayerMaskDrawer 从 URP 设置自动读取。
        // 2022.3：RenderingLayerMask 即本命名空间下的兼容垫片 Fs.Outline.RenderingLayerMask；
        // Unity 6：经上方 using 别名指向引擎内置的 UnityEngine.RenderingLayerMask。
        [Tooltip("通过渲染层（Rendering Layers）控制哪些物体会被描边。")]
        public RenderingLayerMask RenderingLayerMask = 2u;
    }
}
