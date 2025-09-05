using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(OutlineVolumeComponent))]
public class OutlineVolumeComponentEditor : VolumeComponentEditor
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

        var comp = (OutlineVolumeComponent)target;
        if (RenderingLayerMaskUtil.IsHaveLayer)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(13f);
            // 勾选框，控制 overrideState。
            comp.outlineRenderingLayerMask.overrideState = 
                EditorGUILayout.Toggle(comp.outlineRenderingLayerMask.overrideState, GUILayout.Width(15f));
            // MaskField。
            int mask = (int)comp.outlineRenderingLayerMask.value;
            mask = EditorGUILayout.MaskField("Outline Rendering Layer Mask", mask, RenderingLayerMaskUtil.layerNames);
            comp.outlineRenderingLayerMask.value = unchecked((uint)mask);
            EditorGUILayout.EndHorizontal();
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(comp);
        }
    }
}