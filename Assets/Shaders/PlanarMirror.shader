// 鏡用シェーダー。
// PlanarMirror.cs が作る「視点カメラを鏡面で鏡映しした反射カメラ」のRenderTextureを、
// 鏡面を描いている視点カメラのスクリーン座標でそのままサンプリングして貼る(本物の鏡の再現)。
// 反射カメラはLookRotationで作った右手系の普通のカメラなので、本物の鏡像とは左右が逆。
// だからUを反転する(_FlipU、既定ON)。
Shader "NADONIEZU/PlanarMirror"
{
    Properties
    {
        _ReflectionTex ("Mirror View", 2D) = "gray" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        [Toggle] _FlipU ("Flip U (左右が逆に見えるなら切り替える。既定ON)", Float) = 1
        [Toggle] _FlipV ("Flip V (上下が逆に見えるなら)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float4 positionNDC : TEXCOORD0;
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            // SRP Batcher対応: マテリアルごとの値は必ずこのCBUFFERに入れ、Properties{}にも宣言する。
            // どちらか欠けるとSetFloat/SetColorが実質無視される罠を踏む。
            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _FlipU;
                float _FlipV;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionNDC = vpi.positionNDC;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionNDC.xy / IN.positionNDC.w;
                if (_FlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipV > 0.5) uv.y = 1.0 - uv.y;
                return SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv) * _TintColor;
            }
            ENDHLSL
        }
    }
}
