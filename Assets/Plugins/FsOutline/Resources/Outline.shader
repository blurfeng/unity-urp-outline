// References:
// https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-features/how-to-fullscreen-blit.html

Shader "Custom/Outline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Range(0.0, 0.05)) = 0.004
        _OutlineOpacity("Outline Opacity", Range(0.0, 1.0)) = 1.0
        _OutlineHardness("Outline Hardness", Range(0.25, 4.0)) = 1.0
        _OutlinePenetration("Outline Penetration", Range(0.05, 1.0)) = 0.5
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
            half _OutlineOpacity;
            half _OutlineHardness;
            half _OutlinePenetration;

            Varying vert(Attribute IN)
            {
                Varying OUT;

                // 使用方法库函数生成全屏三角形顶点位置和UV坐标。
                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);

                // 计算屏幕宽高比，确保偏移量在不同分辨率下一致。
                const half correction = _ScreenParams.x / _ScreenParams.y;

                // 计算采样偏移量，形成一个环绕当前像素的采样点阵列。
                // 0.707是1/sqrt(2)，用于对角线方向的缩放。最终所有方向的偏移量长度都为_OutlineWidth。
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
                // Sobel 算子核。
                const half kernel_y[8] = {
                    -1, -2, -1,
                     0,      0,
                     1,  2,  1,
                };
                const half kernel_x[8] = {
                    -1,  0,  1,
                    -2,      2,
                    -1,  0,  1,
                };

                // 使用 Sobel 算子计算边缘强度。
                // 采样遮罩的覆盖度（alpha）而非颜色：物体内部 alpha=1、外部=0，与物体颜色无关，
                // 避免纯蓝/纯绿/暗色物体因红通道无对比而检测不到轮廓。
                half gx = 0; half gy = 0;
                for (int i = 0; i < 8; i++)
                {
                    half mask = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, IN.uv + IN.offsets[i]).a;
                    gx += mask * kernel_x[i];
                    gy += mask * kernel_y[i];
                }

                // 读取原始遮罩的 alpha 通道，以确保描边不会覆盖物体本身。
                const half alpha = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, IN.uv).a;
                
                half4 col = _OutlineColor;

                // 边缘强度：Sobel 梯度幅值。_OutlineHardness 作为幂次整形，>1 更锐利、<1 更柔和。
                half edge = saturate(abs(gx) + abs(gy));
                edge = pow(edge, _OutlineHardness);

                // 内部衰减：确保描边不覆盖物体本身。物体外部（alpha=0）为满，向内部渐隐；
                // _OutlinePenetration 为衰减到 0 处的覆盖度，越大向内部渗入越深（0.5 等价旧版 1 - alpha*2）。
                half inner = saturate(1.0 - alpha / _OutlinePenetration);

                // 叠加整体不透明度。
                col.a = edge * inner * _OutlineOpacity;
                
                return col;
            }
            
            ENDHLSL
        }
    }
}