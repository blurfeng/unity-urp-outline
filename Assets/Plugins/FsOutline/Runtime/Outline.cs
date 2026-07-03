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
        
        public ColorParameter color = new ColorParameter(new Color(4f,4f,2f, 1f), true, true, true);

        public ClampedFloatParameter width = new ClampedFloatParameter(0.002f, 0.001f, 0.05f);

        public ClampedFloatParameter opacity = new ClampedFloatParameter(1f, 0f, 1f);

        public ClampedFloatParameter hardness = new ClampedFloatParameter(1f, 0.25f, 4f);

        public ClampedFloatParameter penetration = new ClampedFloatParameter(0.5f, 0.05f, 1f);

        [HideInInspector]
        public UIntParameter renderingLayerMask = new UIntParameter(2);
    }
}
