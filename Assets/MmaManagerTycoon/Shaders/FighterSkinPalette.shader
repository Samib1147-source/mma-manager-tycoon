// Based on the installed URP sprite-unlit shader; retains sprite tint and flip support.
Shader "MMA/Fighter Skin Palette"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _SkinMask ("Skin mask", 2D) = "black" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
          
            TEXTURE2D(_SkinMask);
            SAMPLER(sampler_SkinMask);

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _SkinSource[8];
                float4 _SkinTarget[8];
                float _SkinEnabled;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_SkinEnabled > 0.5 && SAMPLE_TEXTURE2D(_SkinMask, sampler_SkinMask, input.uv).r > 0.5)
                {
                    // Skin geometry is masked from Blender, including highlights
                    // which share palette colors with other objects.
                    float luminance = dot(texel.rgb, float3(0.2126, 0.7152, 0.0722));
                    float nearest = 1000.0;
                    float3 replacement = texel.rgb;
                    [unroll] for (int i = 0; i < 8; i++)
                    {
                        float distance = abs(luminance - dot(_SkinSource[i].rgb, float3(0.2126, 0.7152, 0.0722)));
                        if (distance < nearest) { nearest = distance; replacement = _SkinTarget[i].rgb; }
                    }
                    texel.rgb = replacement;
                }
                return texel * input.color;
            }
            ENDHLSL
        }
    }
}
