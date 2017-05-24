Shader "Portals/UnlitVR"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile __ STEREO_RENDER

			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			float4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				//fixed4 col = PORTAL_VR_CURRENT_EYE == PORTAL_VR_EYE_LEFT ? 1 : 0;
				//fixed4 col;
				//if (unity_CameraProjection[0][2] < 0) {
				//	col = fixed4(1, 0, 0, 1);
				//} else if (unity_CameraProjection[0][2] > 0) {
				//	col = fixed4(0, 1, 0, 1);
				//} else {
				//	col = fixed4(0, 0, 1, 1);
				//}


				fixed4 col;
#ifdef UNITY_SINGLE_PASS_STEREO
				if (unity_StereoEyeIndex == 0)
				{
					col = fixed4(1, 0, 0, 1);
				}
				else
				{
					col = col = fixed4(0, 1, 0, 1);
				}
#else
				if (unity_CameraProjection[0][2] < 0)
				{
					col = fixed4(1, 0, 0, 1);
				}
				else
				{
					col = col = fixed4(0, 1, 0, 1);
				}
#endif

				return col;
			}
			ENDCG
		}
	}
}
