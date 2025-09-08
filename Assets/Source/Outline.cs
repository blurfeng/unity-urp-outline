using System;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

namespace Volumes
{
    [Serializable]
    public class Outline : VolumeComponent
    {
        public BoolParameter isActive = new BoolParameter(true, true);
        
        public ColorParameter outlineColor = new ColorParameter(new Color(4f,4f,2f, 1f), true, true, true);
        
        public ClampedFloatParameter outlineWidth = new ClampedFloatParameter(0.002f, 0.001f, 0.01f);
        
        [HideInInspector]
        public UIntParameter outlineRenderingLayerMask = new UIntParameter(2);
    }
}
