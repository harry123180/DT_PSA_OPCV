// 部件高亮的透視疊層：不做深度測試，零件被其他模型擋住也看得到；邊緣較亮，看得出輪廓。
// 放在 Resources 資料夾，WebGL 建置一定會帶進去（PythonLinkInspector 用 Resources.Load 取得）。
Shader "PythonLink/Highlight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.05, 0.55)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Highlight"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Always
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half rim = 1 - saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS))));
                half4 c = _Color;
                c.a = saturate(c.a + rim * 0.4);
                return c;
            }
            ENDHLSL
        }
    }
}
