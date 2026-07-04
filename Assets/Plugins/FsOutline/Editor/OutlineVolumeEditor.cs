using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Fs.Outline.Editor
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

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(13f);

            // 勾选框，控制 overrideState。
            bool overrideState =
                EditorGUILayout.Toggle(comp.renderingLayerMask.overrideState, GUILayout.Width(15f));

            int mask = (int)comp.renderingLayerMask.value;
            using (new EditorGUI.DisabledScope(!overrideState))
            {
                // MaskField。渲染层名称从当前生效的 URP 资产自动读取。
                // 用 GUIContent 挂 tooltip：本字段 [HideInInspector] 且为手绘，字段上的 [Tooltip] 不会生效。
                var label = new GUIContent("Rendering Layer Mask", "通过渲染层（Rendering Layers）控制哪些物体会被描边。");
                string[] names = RenderingLayerMaskGUI.GetRenderingLayerMaskNames(mask);
                mask = EditorGUILayout.MaskField(label, mask, names);
            }

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                // 记录 Undo 后再写回，支持撤销并正确标脏保存到 Volume Profile 资产。
                Undo.RecordObject(comp, "Edit Outline Rendering Layer Mask");
                comp.renderingLayerMask.overrideState = overrideState;
                comp.renderingLayerMask.value = unchecked((uint)mask);
                EditorUtility.SetDirty(comp);
            }
        }
    }
}
