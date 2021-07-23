Shader "Custom/Caustics"
{
    Properties
    {
        _MainTex("Main Tex", 2D) = "White" {}
        _Normal("Normal", 2D) = "bump" {}
        _Specular("Specular", Color) = (1, 1, 1, 1)
        _NormalScale("Normal Scale", Range(0, 1)) = 0.5
        _CausticsRatio("Caustics Ratio", Range(0, 1)) = 0.5
        _AlphaScale("Alpha Scale", Range(0, 1)) = 0.5
        _Gloss("Gloss", Range(8.0, 256)) = 20
    }
    SubShader
    {
        pass 
        {
            Tags { "RenderType"= "Opaque" }
            LOD 200
            CGPROGRAM

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct v2f{
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 lightDir : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float4 uv : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _Normal;
            float4 _Normal_ST;
            fixed _CausticsRatio;
            fixed _AlphaScale;
            fixed _NormalScale;
            fixed4 _Specular;
            float _Gloss;

            v2f vert(appdata_tan i){
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.uv.xy = i.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                o.uv.zw = i.texcoord.xy * _Normal_ST.xy + _Normal_ST.zw;
                
                float3 binormal = cross(i.normal, i.tangent.xyz) * i.tangent.w;
                float3x3 rotation = float3x3(i.tangent.xyz, binormal, i.normal);
                o.lightDir = normalize(mul(rotation, ObjSpaceLightDir(i.vertex))).xyz;
                o.viewDir = normalize(mul(rotation, ObjSpaceViewDir(i.vertex))).xyz;
                return o;
            }

            inline float4 mainImage (float2 p){
                float3x3 mat = float3x3(-2, -1, 2, 
                                        3, -2, 1,
                                        1, 2, 2);
                float4 k = float4(0, 0, 0, _Time.y) * 0.5;
                k.xy = p * (sin(k * 0.5).w + 2.0) / 200;
                float f1 = length(0.5 - frac(k.xyw = mul(k.xyw, transpose(0.5 * mat))));
                float f2 = length(0.5 - frac(k.xyw = mul(k.xyw, transpose(0.4 * mat))));
                float f3 = length(0.5 - frac(k.xyw = mul(k.xyw, transpose(0.3 * mat))));
                k = pow(min(min(f1, f2), f3), 7.0) * 25.0 + float4(0, 2, 3, 1) / 6;
                return k * _CausticsRatio;
            }

            fixed4 frag(v2f i) : SV_TARGET{
                fixed3 unpackNormal = tex2D(_Normal, i.uv.zw);
                fixed3 normalDir;
                normalDir.xy = (unpackNormal * 2 - 1) * _NormalScale;
                normalDir.z = sqrt(1.0 - saturate(dot(normalDir.xy, normalDir.xy)));
                fixed3 albedo = tex2D(_MainTex, i.uv.xy);
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * albedo;

                fixed3 diffuse = albedo * max(0, dot(i.lightDir, normalDir));
                fixed3 halfDir = normalize(i.lightDir + i.viewDir);
                fixed3 specular = _Specular * pow(max(0, dot(halfDir, normalDir)), _Gloss);
                float2 fragCoord = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;
                return fixed4(specular + diffuse + ambient + mainImage(fragCoord).xyz, 1);
            }
            ENDCG
        }
    }
}
