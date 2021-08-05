/*
	白浪、海浪等纹理采样；
	反射、折射、焦散、菲涅尔反射效果
*/
Shader "Custom/Ocean"
{
		Properties
	{
		_ShallowColor ("ShallowColor", Color) = (1,1,1,1)
		_DeepColor("DeepColor", Color) = (1, 1, 1, 1)
		_BubbleColor ("BubbleColor", Color) = (1, 1, 1, 1)
		_SpecularColor("SpecularColor", Color) = (1, 1, 1, 1)
		/* phong shading光照参数
		_Gloss ("Gloss", Range(8,256)) = 20
		*/
		_Metalness("Metalness", Range(0, 1)) = 0.5
		_Roughness("roughness", Range(0, 1)) = 0.5
		_SubSurfaceStrength("SubSurfaceStrength", Range(0, 1)) = 0.1
		[HideInInspector]_Displace ("_Displace", 2D) = "white" {}
		[HideInInspector]_Normal("_Normal", 2D) = "white" {}
		[HideInInspector]_Bubbles("_Bubbles", 2D) = "White" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
		LOD 200
		pass {
			CGPROGRAM
			#pragma target 4.0
			#pragma multi_compile_fwdbase
			#pragma shader_feature USE_PATCH_DEBUG
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			#include "./CDLOD-GPU/Struct.compute"


			struct input{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				uint instanceID : SV_INSTANCEID;
				uint vertexID : SV_VERTEXID;
			};

			struct v2f{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				half3 debugCol : TEXCOORD2;
			};

			StructuredBuffer<RenderPatch> patchList;
			uniform float3 worldSize;
			fixed4 _ShallowColor;
			fixed4 _DeepColor;
			fixed4 _BubbleColor;
			fixed4 _SpecularColor;
			sampler2D _Displace;
			sampler2D _Normal;
			sampler2D _Bubbles;
			float4 _Displace_ST;
			// float _Gloss;
			fixed _Metalness;
			fixed _Roughness;
			fixed _SubSurfaceStrength;

			static half3 quadTreeDebugs[6] = {
				half3(0, 1, 0), // 绿
				half3(0, 0, 1), // 蓝
				half3(1, 0, 0), // 红
				half3(1, 1, 0), // 黄
				half3(0, 1, 1), // 蓝
				half3(1, 0, 1), // 紫
			};

			v2f vert(input v){
				v2f o;
				RenderPatch patch = patchList[v.instanceID];
				float scale = pow(2, patch.lodLevel);
				v.vertex.xz *= scale;
				v.vertex.xz += patch.worldPos;
				// TODO:UV映射不正确
				o.uv = TRANSFORM_TEX(v.uv, _Displace);
				float4 displace = tex2Dlod(_Displace, float4(o.uv, 0, 0));
				v.vertex += float4(displace.xyz, 0);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.debugCol = half3(1, 1, 1);
				#if USE_PATCH_DEBUG
				o.debugCol = quadTreeDebugs[patch.lodLevel];
				#endif
				return o;
			}

			// F(V, H) = F0 + (1 - F0) * pow(1 - (l * h)), 5)
			inline half3 FresnelSchlick(half3 col, half ldoth){
				return col + (1 - col) * pow(1 - ldoth, 5);
			}

			// 菲涅尔插值，和FresnelSchlick相似，只是使用参数混合两个变量
			inline half3 FresnelLerp(half3 col1, half3 col2, half2 ndotv){
				half t = pow(1 - ndotv, 5);
				return lerp(col1, col2, t);
			}

			// Fdiffuse = (baseColor / pi) * (1 + (Fd - 1) * pow(1 - dot(n, l), 5)) * (1 + (Fd - 1) * pow(1 - dot(n, v), 5))
			// Fd = 0.5 + 2 * roughness * pow(dot(l, h), 2)
			inline fixed3 DisneyDiffuse(fixed3 baseColor, fixed roughness, half ldoth, half ndotl, half ndotv){
				half fd = 0.5 + 2 * pow(ldoth, 2) * roughness;
				half lightScatter = 1 + (fd - 1) * pow(1 - ndotl, 5);
				half viewScatter = 1 + (fd - 1) * pow(1 - ndotv, 5);
				return UNITY_INV_PI * baseColor * lightScatter * viewScatter;
			}

			// D(H) = r4/(pi * pow(pow(dot(n, h), 2) * (r4 - 1) + 1, 2))
			inline half GGXTerm(fixed r2, half ndoth){
				fixed r4 = r2 * r2;
				half d = (ndoth * r4 - ndoth) * ndoth + 1.0;
				return r4 * UNITY_INV_PI / (d * d + 1e-7);
			}

			inline half GGXVisibilityTerm(fixed r2, half ndotv, half ndotl){
				half lambdaV = ndotl * (ndotv * (1 - r2) + r2);
				half lambdaL = ndotv * (ndotl * (1 - r2) + r2);
				return 0.5 / (lambdaL + lambdaV + 1e-5f);
			}

			inline half SchlickPhaseFunc(fixed3 lightDir, fixed3 normalDir, fixed3 viewDir, fixed roughness){
				return saturate(pow(dot(viewDir, -(lightDir + 0.522 * normalDir)), 5.0)) * roughness;
			}

			// TODO: 重写光照模型
			half4 frag(v2f o) : SV_TARGET{
				// 光照衰减的计算，参见https://zhuanlan.zhihu.com/p/31805436
				// float3 lightCoord = mul(unity_WorldToLight, float4(o.worldPos, 1)).xyz;
				// fixed atten = tex2D(_LightTexture0, dot(lightCoord, lightCoord).rr).UNITY_ATTEN_CHANEL;
				// UNITY_LIGHT_ATTUATION(atten, o, o.worldPos);

				fixed3 normalDir = UnityObjectToWorldNormal(tex2D(_Normal, o.uv).rgb);
				fixed bubbles = tex2D(_Bubbles, o.uv).r;
				fixed3 lightDir = normalize(UnityWorldSpaceLightDir(o.worldPos));
				fixed3 viewDir = normalize(UnityWorldSpaceViewDir(o.worldPos));
				fixed3 reflectDir = reflect(-viewDir, normalDir);
				fixed3 halfDir = normalize(viewDir + lightDir);

				// 计算之后公式所需的所有点乘项
				half ldoth = saturate(dot(lightDir, halfDir));
				half ldotv = saturate(dot(lightDir, viewDir));
				half ndotl = saturate(dot(normalDir, lightDir));
				half ndotv = saturate(dot(normalDir, viewDir));
				half ndoth = saturate(dot(normalDir, halfDir));

				//采样反射探头
				half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectDir, 0);
				half3 envMap = DecodeHDR(rgbm, unity_SpecCube0_HDR);
				fixed3 oceanColor = lerp(_ShallowColor, _DeepColor, ndotl);
				fixed3 bubbleColor =  _LightColor0.rgb * _BubbleColor.rgb * saturate(ndotl);
				fixed3 albedo = lerp(oceanColor, bubbleColor, bubbles);
				half oneMinusReflective = 1 - max(max(albedo.r, albedo.g), albedo.b);

				// 暂时先用phong shading凑活一下
				// fixed fresnel = FresnelSchlick(ldoth);
				// fixed3 specualr = _LightColor0.rgb * _SpecularColor.rgb * pow(saturate(max(0, ndoth)), _Gloss);
				// fixed3 diffuse = lerp(oceanColor, bubbleColor, bubbles);
				// fixed3 col = UNITY_LIGHTMODEL_AMBIENT.rgb + specualr + lerp(diffuse, sky, fresnel);
				fixed r2 = _Roughness * _Roughness;
				half V = GGXVisibilityTerm(r2 , ndotv, ndotl);
				half D = GGXTerm(r2, ndoth);
				half3 F0 = lerp(half3(0.4, 0.4, 0.4), _SpecularColor * albedo, _Metalness);
				half3 F = FresnelSchlick(F0, ldoth);
				half3 specualr = V * D * F;

				fixed3 diffColor = albedo * oneMinusReflective;
				fixed3 diffuse = DisneyDiffuse(diffColor, _Roughness, ldoth, ndotl, ndotv);
				half mips = _Roughness * (1.7 - 0.7 * _Roughness) * 6;
				// 通过掠射角得到更加真实的菲涅尔反射效果，同时考虑了材质粗糙度的影响
				half grazingTerm = saturate(2 - _Roughness - oneMinusReflective);
				half surfaceReduction = 1.0 / (r2 + 1.0);
				half3 indirectiveSpceular = surfaceReduction * envMap.rgb * FresnelLerp(F0, grazingTerm, ndotv);
				// 水体次表面散射, 参考https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-1/
				half3 SubSurfaceScatter = envMap.rgb * SchlickPhaseFunc(lightDir, normalDir, viewDir, _SubSurfaceStrength);

				half3 col = UNITY_LIGHTMODEL_AMBIENT.rgb + UNITY_PI * (specualr + diffuse) * ndotl + indirectiveSpceular + SubSurfaceScatter;
				#if USE_PATCH_DEBUG
				col = o.debugCol;
				#endif
				return half4(col, 1);
			}
			ENDCG
		}
	}
}
