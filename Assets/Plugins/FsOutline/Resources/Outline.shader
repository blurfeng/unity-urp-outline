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

        // ---- Pass 0：全屏解析 ----
        // 对遮罩覆盖度做 Sobel 得到描边，按需做遮挡剔除，再混合回相机颜色目标。
        Pass
        {
            Name "OutlineResolve"

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            // 提供 SampleSceneDepth（场景最前不透明表面深度），遮挡剔除时用于与目标自身深度比较。
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

            // 目标自身深度：只包含被描边物体（由遮罩绘制阶段 ZWrite 写入的真实深度），与场景其余物体无关。
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

            // 该 UV 处的“目标表面”是否可见：即目标自身深度处，没有更靠前的不透明物体挡在它前面。
            // 注意：判定发生在“被覆盖的采样点（物体所在处）”，而非描边像素本身——这样即便描边向外
            // 扩出遮挡者边缘，只要它所描的那段物体被挡住，依然会被正确隐藏。返回 1 可见、0 被遮挡。
            half OutlineSurfaceVisible(float2 uv)
            {
                float objDepth = SampleMaskEyeDepth(uv);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                // 场景最前表面不明显近于目标 => 目标可见。留 1% 相对偏置抗深度精度抖动。
                return step(objDepth * 0.99, sceneDepth);
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

                const bool occlusion = _OutlineOcclusion > 0.5;

                // 遮挡剔除：描边是否可见，取决于它所描的物体表面在被覆盖采样点处是否未被遮挡。
                // 只要中心或任一被覆盖邻域处的目标表面可见，就显示这根描边（物体露出的部分照常描边）。
                half visible = 0;
                UNITY_BRANCH
                if (occlusion && alpha > 0.5)
                    visible = OutlineSurfaceVisible(IN.uv);

                // 使用 Sobel 算子计算边缘强度。采样遮罩的覆盖度（alpha）而非颜色，
                // 得到与物体颜色无关的稳定轮廓。
                half gx = 0; half gy = 0;
                for (int i = 0; i < 8; i++)
                {
                    float2 uv = IN.uv + IN.offsets[i];
                    half mask = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_linear_clamp_OutlineMask, uv).a;
                    gx += mask * kernel_x[i];
                    gy += mask * kernel_y[i];

                    // 遮挡剔除：在被覆盖的邻域采样点（物体所在处）判定目标表面可见性。
                    UNITY_BRANCH
                    if (occlusion && mask > 0.5)
                        visible = max(visible, OutlineSurfaceVisible(uv));
                }

                half4 col = _OutlineColor;

                // 边缘强度：Sobel 梯度幅值。_OutlineHardness 作为幂次整形，>1 更锐利、<1 更柔和。
                half edge = saturate(abs(gx) + abs(gy));
                edge = pow(edge, _OutlineHardness);

                // 内部衰减：确保描边不覆盖物体本身。物体外部（alpha=0）为满，向内部渐隐；
                // _OutlinePenetration 为衰减到 0 处的覆盖度，越大向内部渗入越深（0.5 等价旧版 1 - alpha*2）。
                half inner = saturate(1.0 - alpha / _OutlinePenetration);

                half outlineA = edge * inner;

                // 遮挡剔除：被更靠前不透明物体挡住的物体表面，其描边不绘制。
                // 遮罩保持满覆盖、外轮廓不被切割，因此不会沿遮挡者轮廓产生多余边缘。
                UNITY_BRANCH
                if (occlusion)
                    outlineA *= visible;

                // 叠加整体不透明度。
                col.a = outlineA * _OutlineOpacity;

                return col;
            }

            ENDHLSL
        }

        // ---- Pass 1：遮罩覆盖度 ----
        // 作为 overrideMaterial 用于绘制透明队列的被描边物体：强制输出 alpha=1（几何覆盖度，
        // 与材质自身透明度无关）并 ZWrite 写入真实深度。否则透明材质混合后的低 alpha 会被当成
        // “几乎没有覆盖”，导致透明物体描不出边、也无法参与遮挡判定。
        Pass
        {
            Name "OutlineMaskCoverage"

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vertMask
            #pragma fragment fragMask

            struct AttributesMask
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsMask
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsMask vertMask(AttributesMask IN)
            {
                VaryingsMask OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 fragMask(VaryingsMask IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                // 固定覆盖度 alpha=1；颜色无关紧要（解析只读 alpha）。
                return half4(0, 0, 0, 1);
            }

            ENDHLSL
        }
    }
}
