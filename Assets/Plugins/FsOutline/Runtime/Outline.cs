using System;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

namespace Fs.Outline
{
    [Serializable]
    public class Outline : VolumeComponent
    {
        public BoolParameter isActive = new BoolParameter(true, true);
        
        public ColorParameter outlineColor = new ColorParameter(new Color(4f,4f,2f, 1f), true, true, true);
        
        public ClampedFloatParameter outlineWidth = new ClampedFloatParameter(0.002f, 0.001f, 0.05f);

        public ClampedFloatParameter outlineOpacity = new ClampedFloatParameter(1f, 0f, 1f);

        public ClampedFloatParameter outlineHardness = new ClampedFloatParameter(1f, 0.25f, 4f);

        public ClampedFloatParameter outlinePenetration = new ClampedFloatParameter(0.5f, 0.05f, 1f);

        [HideInInspector]
        public UIntParameter outlineRenderingLayerMask = new UIntParameter(2);
    }
}
