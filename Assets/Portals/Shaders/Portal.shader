Shader "Portal/Portal"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry-100"}
		LOD 100

		Pass
		{
			// Don't render anything on the front side. We'll just overwrite it with
			// another camera
			ZWrite on
			ColorMask 0
			Cull Back
		}
		Pass
		{
			// Clear the depth buffer on this geometry so we'll draw everything behind this
			ZWrite on
			ZTest Always
			ColorMask 0
			Cull Front

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct vertexInput
			{
				float4 vertex : POSITION;
			};

			struct fragmentInput
			{
				float4 pos : SV_POSITION;
			};

			struct fragmentOutput
			{
				float4 color : COLOR;
				float depth : DEPTH;
			};


			// functions

			fragmentInput vert(vertexInput i)
			{
				fragmentInput o;
				o.pos = mul(UNITY_MATRIX_MVP, i.vertex);
				return o;
			}

			fragmentOutput frag(fragmentInput i)
			{
				fragmentOutput o;
				o.color = 0.0;
				o.depth = 0.0;
				return o;
			}

			ENDCG
		}
	}
}
