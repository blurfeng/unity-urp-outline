using System;
using UnityEngine;

[Serializable]
public class OutlineSettings
{
    [ColorUsage(true, true)]
    public Color outlineColor = Color.white;
    [Range(0.001f, 0.01f)] public float outlineWidth = 0.002f;
    public ERenderingLayerMask outlineRenderingLayerMask = ERenderingLayerMask.Outline;
}