using System;
using System.Drawing;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

[Serializable]
public class OutlineVolumeComponent : VolumeComponent
{
    public BoolParameter isActive = new BoolParameter(true);
    public ColorParameter outlineColor = new ColorParameter(Color.white, true, true, true);
    public ClampedFloatParameter outlineWidth = new ClampedFloatParameter(0.002f, 0.001f, 0.01f);
    [HideInInspector]
    public UIntParameter outlineRenderingLayerMask = new UIntParameter(2);
}