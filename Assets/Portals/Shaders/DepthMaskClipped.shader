// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Portal/DepthMaskClipped"
{
	Properties
	{
		_ClippingPlane ("Clipping Plane", Vector) = (0, 0, 1, 0)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ColorMask 0
			ZWrite On
			Offset -100000.0, -1.0
			Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 screenPos : SV_POSITION;
				float4 worldPos : TEXCOORD0;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.screenPos = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				return o;
			}

			float3 distanceToPlane(float4 plane, float3 position) {
				return (dot(plane.xyz, position.xyz) + plane.w) / sqrt(dot(plane.xyz, plane.xyz));
			}

			float4 _ClippingPlane;

			fixed4 frag (v2f i) : SV_Target
			{
				clip(distanceToPlane(_ClippingPlane, i.worldPos));
				return float4(i.worldPos.x, 0, 0, 1);
			}
			ENDCG
		}
	}
}
