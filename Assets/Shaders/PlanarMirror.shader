// 鏡用シェーダー(証明写真/監視カメラ方式)。
// PlanarMirror.cs が「鏡の部屋側からプレイヤーを見ているカメラ」のRenderTextureを作るので、
// ここではそれをメッシュ自身のUV座標でそのまま貼るだけ。スクリーン座標合わせ(視差込みの本物の鏡)は
// ゲームプレイ用カメラの肩越しオフセットと相性が悪かったのでやめた。
Shader "NADONIEZU/PlanarMirror"
{
    Properties
    {
        _ReflectionTex ("Mirror View", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        [Toggle] _FlipU ("Flip U (左右が逆に見えるなら。鏡カメラは正面から見た絵なので既定でON)", Float) = 1
        [Toggle] _FlipV ("Flip V (映像が上下逆なら)", Float) = 0
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
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            // SRP Batcher対応: マテリアルごとの値は必ずこのCBUFFERに入れる。
            // ここに入れないとSetFloat/SetColorが実質無視され、値が反映されない・警告が出るという罠を踏む。
            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _FlipU;
                float _FlipV;
                // インポート元のUV0は共有アトラス前提で使えないため、PlanarMirror.csが
                // ローカル座標のbounds(xy: min, zw: size)をここに渡し、そこからUVを作り直す
                float4 _LocalUvRect;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = (IN.positionOS.xy - _LocalUvRect.xy) / _LocalUvRect.zw;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                if (_FlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipV > 0.5) uv.y = 1.0 - uv.y;
                half4 col = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv) * _TintColor;
                return col;
            }
            ENDHLSL
        }
    }
}
