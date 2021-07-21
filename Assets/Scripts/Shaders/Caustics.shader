Shader "Custom/Caustics"
{
    Properties
    {
        _CausticsRatio("Caustics Ratio", Range(0, 1)) = 0.5
    }
    SubShader
    {
        pass 
        {
            // TODO: 两个pass第一个负责物体贴图、高光等效果，第二个pass负责透明的焦散效果
            Tags { "RenderType"= "transparent" }
            LOD 200
            CGPROGRAM

            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct v2f{
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            fixed _CausticsRatio;

            v2f vert(appdata_base i){
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
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

            fixed4 frag(v2f i) : COLOR0{
                float2 fragCoord = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;
                return mainImage(fragCoord).xyz;
            }
            ENDCG
        }
    }
}
