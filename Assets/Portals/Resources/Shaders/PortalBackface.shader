Shader "Portal/PortalBackface"
{
	Properties
	{
		_DefaultTexture("DefaultTexture", 2D) = "white" {}
		_LeftEyeTexture("LeftEyeTexture", 2D) = "white" {}
		_RightEyeTexture("RightEyeTexture", 2D) = "white" {}
		_TransparencyMask("TransparencyMask", 2D) = "white" {}
		_BackfaceAlpha("BackfaceAlpha", Float) = 0
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent" "Queue" = "Transparent+1" }

		Pass
		{
			// Stencil prevents the backface from rendering if we've already seen the frontface
			// Don't render unless Stencil == 0. The value is decremented when we fail (saw a frontface)
			// so that a recursive layer above us won't be prevented from rendering.
			Stencil{
				Ref 0
				Comp Equal
				Fail DecrSat
			}
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest Always
			Lighting Off

			CGPROGRAM
			#define IS_BACKFACE

			#include "PortalVRHelpers.cginc"

			#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

			#pragma vertex vertPortal
			#pragma fragment fragPortal
			ENDCG
		}
	}
	FallBack "VertexLit"
}
