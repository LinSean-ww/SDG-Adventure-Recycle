Shader "Factory/World Projected Floor"
{
    Properties
    {
        _MainTex ("Floor Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TileSize ("Meters Per Tile", Range(0.5, 20)) = 3.5
        _Metallic ("Metallic", Range(0,1)) = 0.2
        _Glossiness ("Smoothness", Range(0,1)) = 0.28
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        float _TileSize;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz / max(_TileSize, 0.01);
            fixed4 color = tex2D(_MainTex, uv) * _Color;
            o.Albedo = color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
