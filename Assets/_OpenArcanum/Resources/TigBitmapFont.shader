Shader "OpenArcanum/UI/TIG Bitmap Font"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ClipRect;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                // TIG's font blitter uses the palette red byte as coverage and blends constant tint with
                // (coverage / 256) in framebuffer byte space. Unity's default UI shader instead applies the
                // byte as a linear alpha in a Linear-color-space project, which makes partial edges too bright.
                float coverageGamma = saturate(tex2D(_MainTex, input.texcoord).a * input.color.a * (255.0 / 256.0));
                float blendAlpha;
                float3 sourceColor;

                #if defined(UNITY_COLORSPACE_GAMMA)
                    blendAlpha = coverageGamma;
                    sourceColor = input.color.rgb;
                #else
                    float3 tintGamma = float3(
                        LinearToGammaSpaceExact(input.color.r),
                        LinearToGammaSpaceExact(input.color.g),
                        LinearToGammaSpaceExact(input.color.b));
                    blendAlpha = GammaToLinearSpaceExact(coverageGamma);
                    float3 tintedCoverageGamma = tintGamma * coverageGamma;
                    float3 premultipliedLinear = float3(
                        GammaToLinearSpaceExact(tintedCoverageGamma.r),
                        GammaToLinearSpaceExact(tintedCoverageGamma.g),
                        GammaToLinearSpaceExact(tintedCoverageGamma.b));
                    sourceColor = blendAlpha > 0.0 ? premultipliedLinear / blendAlpha : 0.0;
                #endif

                float4 color = float4(sourceColor, blendAlpha);
                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
