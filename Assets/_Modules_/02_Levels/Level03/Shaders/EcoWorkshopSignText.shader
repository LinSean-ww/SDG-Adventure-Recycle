Shader "SDGAdventure/EcoWorkshopSignText"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.texcoord).a * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
