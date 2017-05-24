//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

Shader "Custom/StereoRenderShader_5_3"
{
	Properties
	{
		_MainTexture("Albedo (RGB)", 2D) = "white" {}
	}

	CGINCLUDE
	#include "UnityCG.cginc"
	ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Opaque" }

		Pass
		{
			//Cull OFF

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_fwdbase
			#pragma target 3.0

			struct v2f
			{
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata_full i, out float4 outpos : SV_POSITION)
			{
				v2f o;
				outpos = mul(UNITY_MATRIX_MVP, i.vertex);

				o.uv = outpos.xy;
				return o;
			}

			uniform sampler2D _MainTexture;

			fixed4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
			{
				float2 screenUV = screenPos.xy / _ScreenParams.xy;
				return tex2D(_MainTexture, screenUV);;
			}

			ENDCG
		}
	}

	FallBack "Diffuse"
}
