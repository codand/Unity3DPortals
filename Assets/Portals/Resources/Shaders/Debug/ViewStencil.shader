Shader "Portals/Debug/ViewStencil"
{
	Properties
	{
//		_MainTex ("Texture", 2D) = "white" {}
		_StencilRef ("Stencil Ref", float) = 0
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" "Queue" = "Geometry" }
		ZTest Always
		Pass
		{
			Stencil{
				Ref [_StencilRef]
				Comp Equal
			}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			float _StencilRef;
			float4 _Color;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				return _Color;
			}
			ENDCG
		}
	}
}
