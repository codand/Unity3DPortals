Shader "Portal/PortalRenderTexture"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
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
			#pragma target 3.0

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			// note: no SV_POSITION in this struct
			struct v2f {
				float2 uv : TEXCOORD0;
				float2 screenPos : TEXCOORD1;
			};

			v2f vert(
				appdata v,
				out float4 outpos : SV_POSITION // clip space position output
				)
			{
				v2f o;
				float4 worldPos = mul(UNITY_MATRIX_MVP, v.vertex);
				o.screenPos = ComputeScreenPos(worldPos);
				o.uv = v.uv;
				outpos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			sampler2D _MainTex;

			fixed4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
			{
				float x = (screenPos.x / _ScreenParams.x);
				float y = (screenPos.y / _ScreenParams.y);
				if (_ProjectionParams.x > 0)
					y = 1 - y;

				//fixed4 c = tex2D(_MainTex, i.uv);
				
				fixed4 c = tex2D(_MainTex, float2(x, y));
				return c;
			}
			ENDCG
		}
	}
}
