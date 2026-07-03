using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fs.Outline.Editor
{
    /// <summary>
    /// 渲染层名称的编辑器辅助：从当前生效的 URP 资产自动读取已配置的 Rendering Layers 名称，
    /// 取代过去手写的 ERenderingLayer 枚举。供 RenderingLayerMaskDrawer 与 OutlineVolumeEditor 共用。
    /// 本类不做版本隔离，Unity 2022.3 与 Unity 6 均可用。
    /// </summary>
    public static class RenderingLayerMaskGUI
    {
        /// <summary>
        /// 读取 URP 的渲染层名称，并按 URP 原生行为补足未定义位（“Unused Layer N”）。
        /// URP 14 的 UniversalRenderPipelineGlobalSettings 为 internal（跨程序集不可访问），
        /// 因此改从当前生效的 URP 资产取名称——UniversalRenderPipelineAsset.renderingLayerMaskNames 是 public，
        /// 其内部转发到 Global Settings，效果一致。
        /// </summary>
        public static string[] GetRenderingLayerMaskNames(int mask)
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            string[] names = urpAsset ? urpAsset.renderingLayerMaskNames : null;
            if (names == null || names.Length == 0)
                names = new[] { "Default" };

            // 若掩码用到的最高位超过已定义名称数量，补 “Unused Layer N”，与 URP 一致。
            int maskCount = mask > 0 ? (int)Mathf.Log(mask, 2) + 1 : 0;
            if (mask > 0 && names.Length < maskCount && maskCount <= 32)
            {
                var padded = new string[maskCount];
                for (int i = 0; i < maskCount; ++i)
                    padded[i] = i < names.Length ? names[i] : $"Unused Layer {i}";
                names = padded;
            }

            return names;
        }
    }
}
