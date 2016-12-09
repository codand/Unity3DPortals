Shader "Portal/PortalRenderTexture"
{
	Properties
	{
		_MainTex ("MainTex", 2D) = "white" {}
	}

	SubShader
	{
		//LOD 100

		//Pass
		//{
		//	Tags{ "RenderType" = "Opaque" "LightMode" = "Deferred"}
		//	Offset -1.0, -1.0
		//	ZWrite on

		//	CGPROGRAM
		//	#pragma vertex vert
		//	#pragma fragment frag

		//	#include "UnityCG.cginc"

		//	struct appdata
		//	{
		//		float4 vertex : POSITION;
		//	};

		//	struct v2f {
		//		float4 pos : SV_POSITION;
		//		float4 screenPos : TEXCOORD0;
		//	};

		//	v2f vert(appdata v)
		//	{
		//		v2f o;
		//		o.pos = UnityObjectToClipPos(v.vertex);
		//		o.screenPos = ComputeScreenPos(o.pos);
		//		return o;
		//	}

		//	sampler2D _Depth;
		//	sampler2D _GBuf0;
		//	sampler2D _GBuf1;
		//	sampler2D _GBuf2;
		//	sampler2D _GBuf3;

		//	void frag(
		//		v2f i,
		//		out float outDepth : SV_DEPTH,
		//		out half4 outGBuffer0 : SV_Target0, // RT0, ARGB32 format: Diffuse color (RGB), occlusion (A).
		//		out half4 outGBuffer1 : SV_Target1, // RT1, ARGB32 format: Specular color (RGB), roughness (A).
		//		out half4 outGBuffer2 : SV_Target2, // RT2, ARGB2101010 format: World space normal (RGB), unused (A).
		//		out half4 outEmission : SV_Target3	// RT3, emission (rgb), --unused-- (a) |~ Or this one ~| RT3, ARGB2101010 (non-HDR) or ARGBHalf (HDR) format: Emission + lighting + lightmaps + reflection probes buffer.
		//		)
		//	{
		//		// TODO: figure out how to offset this correctly to avoid Z fighting.
		//		outDepth = tex2Dproj(_Depth, UNITY_PROJ_COORD(i.screenPos)) * 1.0001;
		//		outGBuffer0 = tex2Dproj(_GBuf0, UNITY_PROJ_COORD(i.screenPos));
		//		outGBuffer1 = tex2Dproj(_GBuf1, UNITY_PROJ_COORD(i.screenPos));
		//		outGBuffer2 = tex2Dproj(_GBuf2, UNITY_PROJ_COORD(i.screenPos));
		//		outEmission = tex2Dproj(_GBuf3, UNITY_PROJ_COORD(i.screenPos));
		//	}
		//	ENDCG
		//}

		Pass
		{
			Tags{ "RenderType" = "Opaque" }
			Offset -1, -1.0
			//Offset -0.1, -0.1

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma multi_compile_fog
			#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0;
				//UNITY_FOG_COORDS(1)
			};

#ifdef SAMPLE_PREVIOUS_FRAME
			float4x4 PORTAL_MATRIX_VP;
#endif
			sampler2D _MainTex;

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
#ifdef SAMPLE_PREVIOUS_FRAME
				// Instead of getting the clip position of our portal from the currently rendering camera,
				// calculate the clip position of the portal from a higher level portal. PORTAL_MATRIX_VP == camera.projectionMatrix.
				float4 recursionClipPos = mul(PORTAL_MATRIX_VP, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)));

				// TODO: Figure out how to get this value properly (https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html)
				_ProjectionParams.x = 1;
				o.uv = ComputeScreenPos(recursionClipPos);
#else
				o.uv = ComputeScreenPos(o.pos);
#endif
				//UNITY_TRANSFER_FOG(o, o.pos);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2Dproj(_MainTex, UNITY_PROJ_COORD(i.uv));
				return col;
			}
			ENDCG
		}
	}
	FallBack "VertexLit"
}
