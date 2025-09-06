using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

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
        
            if (RenderingLayerMaskUtil.IsHaveLayer)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(13f);
            
                // 勾选框，控制 overrideState。
                comp.outlineRenderingLayerMask.overrideState = 
                    EditorGUILayout.Toggle(comp.outlineRenderingLayerMask.overrideState, GUILayout.Width(15f));
            
                EditorGUI.BeginDisabledGroup(!comp.outlineRenderingLayerMask.overrideState);
                // MaskField。
                int mask = (int)comp.outlineRenderingLayerMask.value;
                mask = EditorGUILayout.MaskField("Outline Rendering Layer Mask", mask, RenderingLayerMaskUtil.layerNames);
                comp.outlineRenderingLayerMask.value = unchecked((uint)mask);
                EditorGUI.EndDisabledGroup();
            
                EditorGUILayout.EndHorizontal();
            }
        
            if (GUI.changed)
            {
                EditorUtility.SetDirty(comp);
            }
        }
    }
}