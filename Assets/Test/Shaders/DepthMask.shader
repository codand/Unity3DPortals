Shader "Portals/DepthMask"
{
	SubShader
	{
		Pass
		{
			Tags{ "RenderType" = "Opaque" }
			Stencil{
				Ref 5
				Comp Equal
			}

			ZWrite On
			ZTest Always
			ColorMask 0

			CGPROGRAM
			#include "UnityCG.cginc"

			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_shadowcaster

			float4 vert(in float4 vertex : POSITION) : POSITION {
				// Bias scales the portal slightly when rendering depth to prevent shadow bleeding
				//float bias = 1.01;
				//return UnityObjectToClipPos(vertex * bias);
				return UnityObjectToClipPos(vertex);
			}

			float frag() : DEPTH{
				return 0;
			}
			ENDCG
		}
	}
	FallBack "VertexLit"
}
