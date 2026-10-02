Shader "UI/RadialEmissive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Tint", Color) = (1,1,1,1)

        [HDR] _EmissionColor ("Emission Color", Color) = (0,1,1,1)
        _EmissionStrength ("Emission Strength", Range(0,10)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)]
        _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;

            fixed4 _EmissionColor;
            float _EmissionStrength;

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;

                OUT.vertex =
                    UnityObjectToClipPos(v.vertex);

                OUT.texcoord =
                    TRANSFORM_TEX(
                        v.texcoord,
                        _MainTex
                    );

                // Keeps the Graphic.color field working.
                OUT.color =
                    v.color * _Color;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texColor =
                    tex2D(
                        _MainTex,
                        IN.texcoord
                    );

                fixed4 baseColor =
                    texColor * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                    baseColor.a *=
                        UnityGet2DClipping(
                            IN.worldPosition.xy,
                            _ClipRect
                        );
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(baseColor.a - 0.001);
                #endif

                // Emission is added on top of the base RGB.
                // Keep alpha controlled by the actual UI graphic.
                fixed3 emission =
                    _EmissionColor.rgb *
                    _EmissionStrength;

                fixed3 finalRGB =
                    baseColor.rgb +
                    emission * baseColor.a;

                return fixed4(
                    finalRGB,
                    baseColor.a
                );
            }

            ENDHLSL
        }
    }
}