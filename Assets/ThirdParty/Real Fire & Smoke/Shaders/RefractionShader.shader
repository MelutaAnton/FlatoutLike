Shader "Real Fire & Smoke/RefractionShader" {
    Properties {
        _BumpAmt ("Distortion", range(0,128)) = 10
        _MainTex ("Tint Color (RGB)", 2D) = "white" {}
        _BumpMap ("Normalmap", 2D) = "bump" {}
    }

    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass {
            Name "GrabPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uvgrab : TEXCOORD0;
                float2 uvbump : TEXCOORD1;
                float2 uvmain : TEXCOORD2;
                float fogCoord : TEXCOORD3;
            };

            TEXTURE2D(_GrabTexture);
            SAMPLER(sampler_GrabTexture);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _GrabTexture_TexelSize;
            float _BumpAmt;
            float4 _BumpMap_ST;
            float4 _MainTex_ST;

            Varyings vert(Attributes v) {
			Varyings o;
			o.positionHCS = TransformObjectToHClip(v.positionOS); // Fixed truncation error
			o.uvgrab = ComputeScreenPos(o.positionHCS); // Use grabPos for proper sampling
			o.uvbump = TRANSFORM_TEX(v.uv, _BumpMap);
			o.uvmain = TRANSFORM_TEX(v.uv, _MainTex);
			return o;
		}

		half4 frag(Varyings i) : SV_Target {
			// Calculate perturbed coordinates
			half2 bump = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uvbump)).rg;
			float2 offset = bump * _BumpAmt * _GrabTexture_TexelSize.xy;
			i.uvgrab.xy += offset;

			// Use ComputeScreenPos result for sampling
			float4 grabPos = ComputeScreenPos(i.positionHCS);
			half4 col = SAMPLE_TEXTURE2D(_GrabTexture, sampler_GrabTexture, grabPos.xy / grabPos.w);

			// Tint the result
			half4 tint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uvmain);
			col *= tint;

			return col;
		}


            ENDHLSL
        }

        // Fallback
        Pass {
            Name "BASE"
            Blend DstColor Zero
            SetTexture [_MainTex] { combine texture }
        }
    }
}