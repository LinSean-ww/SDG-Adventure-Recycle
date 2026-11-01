Shader "C3/V11HintOverlay"
{
    Properties { [HDR] _EmissionColor ("Hint emission", Color) = (0,0,0,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _EMISSION
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; };
            half4 _EmissionColor;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                #ifdef _EMISSION
                return half4(_EmissionColor.rgb, 0);
                #else
                return 0;
                #endif
            }
            ENDCG
        }
    }
    Fallback Off
}
