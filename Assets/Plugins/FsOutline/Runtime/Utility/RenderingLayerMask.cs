#if !UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Fs.Outline
{
    /// <summary>
    /// 渲染层遮罩（Unity 2022.3 兼容垫片）。
    /// Unity 6（URP16 / 2023.1+）内置 UnityEngine.RenderingLayerMask 结构体，并自带 Inspector 遮罩下拉；
    /// Unity 2022.3 没有该类型。本结构体是最小替身：内部就是一个 uint 位掩码，与 uint 隐式互转，
    /// 可直接用作 FilteringSettings.renderingLayerMask；配套的 RenderingLayerMaskDrawer 从当前 URP 资产
    /// 自动读取已配置的渲染层名称并绘制成勾选式下拉。U6 不编译本文件（改用引擎内置类型）。
    /// </summary>
    [Serializable]
    public struct RenderingLayerMask
    {
        // 位掩码本体。字段名 _bits 供 RenderingLayerMaskDrawer 通过 FindPropertyRelative("_bits") 访问。
        [SerializeField] private uint _bits;

        public RenderingLayerMask(uint value) { _bits = value; }

        /// <summary>位掩码值。</summary>
        public uint Value { get => _bits; set => _bits = value; }

        // 与 uint 隐式互转：让本类型能无缝喂给 FilteringSettings(...) 并与 renderingLayerMask(uint) 直接比较。
        // ⚠ 故意不定义 == / != 运算符——一旦定义，“uint != RenderingLayerMask” 会在内置 uint 比较与
        //   自定义运算符（uint 隐式转 RenderingLayerMask）之间产生二义（CS0034）。仅保留隐式转换，
        //   编译器就会把混合比较解析为内置 uint 比较，行为正确且无歧义。
        public static implicit operator uint(RenderingLayerMask mask) => mask._bits;
        public static implicit operator RenderingLayerMask(uint value) => new RenderingLayerMask(value);

        public override string ToString() => _bits.ToString();
    }
}
#endif
