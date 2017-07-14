Shader "Portal/Portal"
{
	Properties
	{
		_DefaultTexture("DefaultTexture", 2D) = "white" {}
		_LeftEyeTexture("LeftEyeTexture", 2D) = "white" {}
		_RightEyeTexture("RightEyeTexture", 2D) = "white" {}
		_TransparencyMask("TransparencyMask", 2D) = "white" {}
	}

	SubShader
	{
		Tags{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
		}

		// Mask off the entire shape so we can't see the backface if the frontface is visible
		// For some reason, ZWrite doesn't appear to work if we use it in our main pass, so
		// we need to use this priming pass.
		//Pass {
		//	//ZWrite On
		//	//ColorMask 0
		//	Stencil {
		//		Comp Always
		//		Pass IncrSat
		//	}
		//}

		Pass
		{
			// Stencil prevents the backface from rendering if we've already seen the frontface
			Stencil{
				Comp Always
				Pass IncrSat
			}
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Lighting Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME DONT_SAMPLE
			#pragma multi_compile __ STEREO_RENDER

			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 screenUV : TEXCOORD0;
				float4 objUV : TEXCOORD1;
				UNITY_FOG_COORDS(2)
			};

#ifdef SAMPLE_PREVIOUS_FRAME
			float4x4 PORTAL_MATRIX_VP;
#endif
			sampler2D _DefaultTexture;
			sampler2D _LeftEyeTexture;
			sampler2D _RightEyeTexture;
			sampler2D _TransparencyMask;

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.objUV = v.uv;
#ifdef SAMPLE_PREVIOUS_FRAME
				// Instead of getting the clip position of our portal from the currently rendering camera,
				// calculate the clip position of the portal from a higher level portal. PORTAL_MATRIX_VP == camera.projectionMatrix.
				float4 recursionClipPos = mul(PORTAL_MATRIX_VP, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)));

				// TODO: Figure out how to get this value properly (https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html)
				_ProjectionParams.x = 1;
				o.screenUV = ComputeScreenPos(recursionClipPos);
#else
				o.screenUV = ComputeScreenPos(o.pos);
#endif
				UNITY_TRANSFER_FOG(o, o.pos);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 screenUV = i.screenUV.xy / i.screenUV.w;
				fixed4 col;

#ifdef UNITY_SINGLE_PASS_STEREO
				if (PORTAL_VR_CURRENT_EYE == PORTAL_VR_EYE_LEFT)
				{
					screenUV.x *= 2;
					col = tex2D(_LeftEyeTexture, screenUV);
				}
				else
				{
					screenUV.x = (screenUV.x - 0.5) * 2;
					col = tex2D(_RightEyeTexture, screenUV);
				}
#else
				if (PORTAL_VR_CURRENT_EYE == PORTAL_VR_EYE_LEFT)
				{
					col = tex2D(_LeftEyeTexture, screenUV);
				}
				else
				{
					col = tex2D(_RightEyeTexture, screenUV);
				}
#endif

#ifdef DONT_SAMPLE
				col.rgb = col = tex2D(_DefaultTexture, i.objUV);
#endif
				
				col.a = tex2D(_TransparencyMask, i.objUV).r;
				//UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
	FallBack "VertexLit"
}
