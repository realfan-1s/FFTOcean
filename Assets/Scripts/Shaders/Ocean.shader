﻿/*
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
		_Metalness("Metalness", Range(0, 1)) = 0.5
		_Roughness("roughness", Range(0, 1)) = 0.5
		[Header(SubSurface Scattering)]
		_SubSurfaceColor("SubSurface Color", Color) = (1, 1, 1, 1)
		_ShadowFactor("Shadow Factor", Range(0, 1.0)) = 0.2
		_SubSurfacePower("SubSurface Power", Range(1.0, 2.0)) = 1.7
		_SubSurfaceScale("SurSurface Scale", Range(0.0, 1.0)) = 5
		_Gloss("Gloss", Range(5.0, 20)) = 5.5
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
			#pragma target 4.5
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
			float _Gloss;
			fixed _Metalness;
			fixed _Roughness;
			float _ShadowFactor;
			float _SubSurfacePower;
			float _SubSurfaceScale;
			fixed4 _SubSurfaceColor;

			static half3 quadTreeDebugs[6] = {
				half3(0, 1, 0), // 绿
				half3(0, 0, 1), // 蓝
				half3(1, 0, 0), // 红
				half3(1, 1, 0), // 黄
				half3(0, 1, 1), // 蓝
				half3(1, 0, 1), // 紫
			};

			inline void GetRealUV(inout float2 uv, float2 worldPos, float scale){
				float2 startPos = worldPos - scale * float2(4, 4) + float2(4096, 4096);
				float ratio = scale / 64;
				startPos.x -= (uint)startPos.x / 512 * 512;
				startPos.y -= (uint)startPos.y / 512 * 512;
				uv *= ratio;
				uv += startPos / 512;
			}

			v2f vert(input v, uint instanceID : SV_INSTANCEID){
				v2f o;
				RenderPatch patch = patchList[instanceID];
				float scale = pow(2, patch.lodLevel);
				v.vertex.xz *= scale;
				v.vertex.xz += patch.worldPos;
				GetRealUV(v.uv, patch.worldPos, scale);
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

			/*
			假设光更有可能在波浪的一侧被水散射与透射,基于FFT模拟产生的顶点偏移，为波的侧面生成波峰mask
			根据视角，光源方向和波峰mask的组合，将深水颜色和次表面散射水体颜色之间进行混合，得到次表面散射颜色。
			将位移值（Displacement）除以波长，并用此缩放后的新的位移值计算得出次表面散射项强度。
			*/
			inline half3 CalSSS(fixed3 lightDir, fixed3 normalDir, fixed3 viewDir, fixed4 sssCol,
			float waveHeight, float shadowFactor, float power, float scale, float emssionBase){
				half lightStrength = sqrt(saturate(lightDir.y));
				float fltDot = pow(saturate(dot(normalDir, -lightDir)) + saturate(dot(viewDir, lightDir)), power);
				float fltRatio = fltDot * lightStrength * shadowFactor * emssionBase;
				return sssCol * (waveHeight * 0.6 + fltRatio);
			}

			// TODO:重写着模型
			half4 frag(v2f o) : SV_TARGET{
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
				// 水体次表面散射, https://zhuanlan.zhihu.com/p/95917609
				half3 SubSurfaceScatter = CalSSS(lightDir, normalDir, viewDir, _SubSurfaceColor,
				tex2D(_Displace, o.uv).g, _ShadowFactor, _SubSurfacePower, _SubSurfaceScale, _Gloss) * 0.01;

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
