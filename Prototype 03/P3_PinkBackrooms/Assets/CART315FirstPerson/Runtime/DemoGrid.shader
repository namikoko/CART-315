Shader "CART315/Demo Grid"
{
    Properties { _Color ("Color", Color) = (0.2, 0.3, 0.4, 1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            // An untagged pass is rendered by both Built-in and URP (SRPDefaultUnlit).
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            float4 _Color;
            v2f vert(appdata_base v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = abs(normalize(i.normal));
                float2 uv = n.y > 0.5 ? i.world.xz : (n.x > 0.5 ? i.world.zy : i.world.xy);
                float2 edge = abs(frac(uv - 0.5) - 0.5) / max(fwidth(uv), 0.001);
                float grid = 1 - saturate(min(edge.x, edge.y));
                float shade = 0.55 + 0.45 * saturate(dot(normalize(i.normal), normalize(float3(-0.4, 1, -0.3))));
                return float4(_Color.rgb * shade + grid * 0.065, 1);
            }
            ENDCG
        }
    }
}
