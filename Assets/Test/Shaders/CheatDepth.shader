Shader "Test/CheatDepth"
{
	SubShader
	{
		Tags { "Queue"="Geometry"}
		Cull Off
		ZWrite On

		Pass
		{
			Tags { "LightMode" = "Deferred" }
			CGPROGRAM
			#include "UnityCG.cginc"

			#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

			#pragma vertex vert
			#pragma fragment frag


			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 pos : SV_POSITION;
			};

			struct fragOut {
				fixed4 color : COLOR;
				float depth : DEPTH;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fragOut frag(v2f i)
			{
				fragOut o;
				o.color = fixed4(1,0,0,1);
				o.depth = 0.1;
				return o;
			}
			ENDCG
		}
	}
}
