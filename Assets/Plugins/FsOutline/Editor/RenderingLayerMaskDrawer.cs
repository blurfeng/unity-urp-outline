#if !UNITY_6000_0_OR_NEWER
using UnityEditor;
using UnityEngine;

namespace Fs.Outline.Editor
{
    /// <summary>
    /// Fs.Outline.RenderingLayerMask（Unity 2022.3 兼容垫片）的 Inspector 绘制器。
    /// 从当前 URP 资产读取已配置的渲染层名称，绘制成与 Unity 6 内置一致的勾选式遮罩下拉。
    /// 逻辑复刻自 URP 内部的 EditorUtils.DrawRenderingLayerMask（该方法为 internal 无法直接调用），仅用公有 API。
    /// U6 不编译本文件（引擎内置的 RenderingLayerMask 结构体自带绘制器）。
    /// </summary>
    [CustomPropertyDrawer(typeof(Fs.Outline.RenderingLayerMask))]
    public class RenderingLayerMaskDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty bitsProp = property.FindPropertyRelative("_bits");
            if (bitsProp == null)
            {
                // 兜底：结构体布局异常时退回默认绘制，避免抛异常。
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int mask = unchecked((int)bitsProp.uintValue);
            string[] names = RenderingLayerMaskGUI.GetRenderingLayerMaskNames(mask);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            mask = EditorGUI.MaskField(position, label, mask, names);
            if (EditorGUI.EndChangeCheck())
                bitsProp.uintValue = unchecked((uint)mask);
            EditorGUI.EndProperty();
        }
    }
}
#endif
