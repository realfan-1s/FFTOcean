Shader "Custom/Test"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "./Struct.compute"
            #include "UNITYCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            StructuredBuffer<RenderPatch> patchList;

            struct Input{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint id : SV_INSTANCEID;
            };
            struct v2f{
                float4 pos : SV_POSITION;
                float2 uv:TEXCOORD0;
            };

            v2f vert(Input i){
                v2f o;
                RenderPatch patch = patchList[i.id];
                uint lod = patch.lodLevel;
                float scale = pow(2, lod);
                i.vertex.xz *= scale;
                i.vertex.xz += patch.worldPos;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = i.uv * scale * 8;
                return o;
            }
            half4 frag(v2f o) : SV_TARGET{
                return half4(0, 0, 0, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
