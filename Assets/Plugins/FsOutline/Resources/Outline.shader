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
        _OutlineOcclusion("Occlusion Culling", Float) = 0
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
            // 提供 SampleSceneDepth（场景最前表面深度），遮挡剔除时用于与目标自身深度比较。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            // 目标自身深度：只包含被描边物体（由物体材质 ZWrite 写入的真实深度），与场景其余物体无关。
            // 用点采样避免深度值被线性插值污染。仅遮挡剔除时使用。
            TEXTURE2D_X_FLOAT(_OutlineMaskDepth);
            SAMPLER(sampler_point_clamp_OutlineMaskDepth);

            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineOpacity;
            half _OutlineHardness;
            half _OutlinePenetration;
            half _OutlineOcclusion;

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

            // 采样目标深度并转为线性视空间深度（米）。
            float SampleMaskEyeDepth(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X(_OutlineMaskDepth, sampler_point_clamp_OutlineMaskDepth, uv).r;
                return LinearEyeDepth(raw, _ZBufferParams);
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

                // 读取中心遮罩覆盖度（alpha）。物体内部 alpha=1、外部=0，与物体颜色无关，
                // 避免纯蓝/纯绿/暗色物体因红通道无对比而检测不到轮廓。
                const half alpha = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, IN.uv).a;

                // 仅遮挡剔除开启时才需要采样目标深度。
                const bool occlusion = _OutlineOcclusion > 0.5;

                // targetDepth：邻域内最近的目标表面深度，作为该描边像素处“物体在此的深度”。
                float mDepthCenter = 0;
                UNITY_BRANCH
                if (occlusion)
                    mDepthCenter = SampleMaskEyeDepth(IN.uv);

                float targetDepth = 1e8;
                if (occlusion && alpha > 0.5)
                    targetDepth = mDepthCenter;

                // 使用 Sobel 算子计算边缘强度。采样遮罩的覆盖度（alpha）而非颜色，
                // 得到与物体颜色无关的稳定轮廓。
                half gx = 0; half gy = 0;
                for (int i = 0; i < 8; i++)
                {
                    float2 uv = IN.uv + IN.offsets[i];
                    half mask = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, uv).a;
                    gx += mask * kernel_x[i];
                    gy += mask * kernel_y[i];

                    // 遮挡剔除：记录邻域内最近的目标表面深度。
                    UNITY_BRANCH
                    if (occlusion && mask > 0.5)
                        targetDepth = min(targetDepth, SampleMaskEyeDepth(uv));
                }

                half4 col = _OutlineColor;

                // 边缘强度：Sobel 梯度幅值。_OutlineHardness 作为幂次整形，>1 更锐利、<1 更柔和。
                half edge = saturate(abs(gx) + abs(gy));
                edge = pow(edge, _OutlineHardness);

                // 内部衰减：确保描边不覆盖物体本身。物体外部（alpha=0）为满，向内部渐隐；
                // _OutlinePenetration 为衰减到 0 处的覆盖度，越大向内部渗入越深（0.5 等价旧版 1 - alpha*2）。
                half inner = saturate(1.0 - alpha / _OutlinePenetration);

                half outlineA = edge * inner;

                // 遮挡剔除：若描边像素处场景最前表面明显近于目标（有物体挡在目标之前），隐藏该处描边。
                // 遮罩保持满覆盖、外轮廓不被切割，因此不会沿遮挡者轮廓产生多余边缘——只是被挡住的那圈描边不画。
                UNITY_BRANCH
                if (occlusion)
                {
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(IN.uv), _ZBufferParams);
                    // 可见：场景深度 >= 目标深度（无遮挡者在前）。留 1% 相对偏置抗深度精度抖动。
                    half visible = step(targetDepth * 0.99, sceneDepth);
                    outlineA *= visible;
                }

                // 叠加整体不透明度。
                col.a = outlineA * _OutlineOpacity;

                return col;
            }

            ENDHLSL
        }
    }
}
