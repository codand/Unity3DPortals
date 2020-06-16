Shader "Portal/DepthPunch"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" }


		Pass
		{
			ZWrite On // write to depth
			ZTest Always // render over everything
			ColorMask 0 // only render to depth
			Cull Front
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _TransparencyMask;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			struct output {
				fixed4 color : SV_Target;
				float depth: SV_Depth;
			};


			v2f vert(appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			output frag(v2f i) {
				float alpha = tex2D(_TransparencyMask, i.uv).x;
				clip(alpha - 0.5);

				output o;
#ifdef UNITY_REVERSED_Z
				o.depth = 0; // far projection distance at 0 on dx11, exists so this works in editor
#else
				o.depth = 1;
#endif
				o.color = fixed4(0, 0, 0, 0);
				return o;
			}
			ENDCG
		}
	}
}
