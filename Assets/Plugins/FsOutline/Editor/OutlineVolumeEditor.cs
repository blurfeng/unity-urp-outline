using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Fs.Outline.Editor;

namespace Volumes
{
    [CustomEditor(typeof(Outline))]
    public class OutlineVolumeEditor : VolumeComponentEditor
    {
        public override void OnEnable()
        {
            if (!target)
                return;

            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var comp = (Outline)target;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(13f);

            // 勾选框，控制 overrideState。
            comp.outlineRenderingLayerMask.overrideState =
                EditorGUILayout.Toggle(comp.outlineRenderingLayerMask.overrideState, GUILayout.Width(15f));

            EditorGUI.BeginDisabledGroup(!comp.outlineRenderingLayerMask.overrideState);
            // MaskField。渲染层名称从当前生效的 URP 资产自动读取。
            int mask = (int)comp.outlineRenderingLayerMask.value;
            string[] names = RenderingLayerMaskGUI.GetRenderingLayerMaskNames(mask);
            mask = EditorGUILayout.MaskField("Outline Rendering Layer Mask", mask, names);
            comp.outlineRenderingLayerMask.value = unchecked((uint)mask);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(comp);
            }
        }
    }
}
