// References:
// https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@15.0/manual/renderer-features/how-to-fullscreen-blit.html

Shader "Custom/Outline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Range(0.0, 0.01)) = 0.004
    }
    
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct  Attribute
            {
                uint vertexID : SV_VertexID;
            };

            struct Varying
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 offsets[8] : TEXCOORD1;
            };

            TEXTURE2D_X(_OutlineMask);
            // https://docs.unity3d.com/Manual/SL-SamplerStates.html
            SAMPLER(sampler_linear_clamp_OutlineMask);

            half4 _OutlineColor;
            half _OutlineWidth;

            Varying vert(Attribute IN)
            {
                Varying OUT;

                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);

                const half correction = _ScreenParams.x / _ScreenParams.y;

                OUT.offsets[0] = half2(-1, correction) * 0.707 * _OutlineWidth; // Top-left
                OUT.offsets[1] = half2(0, correction) * _OutlineWidth;  // Top
                OUT.offsets[2] = half2(1, correction) * 0.707 * _OutlineWidth;  // Top-right
                OUT.offsets[3] = half2(-1, 0) * _OutlineWidth; // Left
                
                OUT.offsets[4] = half2(1, 0) * _OutlineWidth;  // Right
                OUT.offsets[5] = half2(-1, -correction) * 0.707 * _OutlineWidth; // Bottom-left
                OUT.offsets[6] = half2(0, -correction) * _OutlineWidth;  // Bottom
                OUT.offsets[7] = half2(1, -correction) * 0.707 * _OutlineWidth;  // Bottom-right

                return OUT;
            }

            half4 frag(Varying IN) : SV_Target
            {
                const half kernelX[8] = {
                        -1,  0,  1,
                        -2,      2,
                        -1,  0,  1,
                };
                const half kernelY[8] = {
                        -1, -2, -1,
                         0,      0,
                         1,  2,  1,
                };
                half gx = 0;
                half gy = 0;
                half mask = 0;
                for (int i = 0; i < 8; i++)
                {
                    mask = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, IN.uv + IN.offsets[i]).r;
                    gx += mask * kernelX[i];
                    gy += mask * kernelY[i];
                }

                const half alpha = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, IN.uv).a;
                half4 col = _OutlineColor;
                // 确保描边不会覆盖物体本身。当物体本身透明度较高时，描边也会变淡。
                col.a = saturate(abs(gx) + abs(gy)) * saturate(1.0 - alpha - 0.5);
                
                //half4 col = 
                return col; // Black outline
            }
            
            ENDHLSL
        }
    }
}