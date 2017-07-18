Shader "Portal/Portal"
{
	Properties
	{
		_DefaultTexture("DefaultTexture", 2D) = "white" {}
		_LeftEyeTexture("LeftEyeTexture", 2D) = "black" {}
		_RightEyeTexture("RightEyeTexture", 2D) = "red" {}
		_TransparencyMask("TransparencyMask", 2D) = "white" {}
	}

	SubShader
	{
		Tags{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
		}

		Pass
		{
			// Stencil prevents the backface from rendering if we've already seen the frontface
			Stencil{
				Comp Always
				Pass IncrSat
				ZFail IncrSat
			}
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Lighting Off

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

			#pragma vertex vertPortal
			#pragma fragment fragPortal
			ENDCG
		}
	}
	FallBack "VertexLit"
}
