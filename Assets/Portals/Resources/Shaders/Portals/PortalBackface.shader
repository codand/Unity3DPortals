Shader "Portals/PortalBackface"
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
		Tags{ "RenderType" = "Opaque" "Queue" = "Transparent+101" }

		//Pass
		//{
		//	// Stencil prevents the backface from rendering if we've already seen the frontface
		//	// Don't render unless Stencil == 0. The value is decremented when we fail (saw a frontface)
		//	// so that a recursive layer above us won't be prevented from rendering.
		//	Stencil{
		//		Ref 0
		//		Comp Equal
		//		Fail DecrSat
		//		Pass IncrSat
		//	}	

		//	Blend One Zero
		//	ZWrite Off
		//	ZTest LEqual
		//	Cull Front
		//	ColorMask 0

		//	CGPROGRAM
		//	#define IS_BACKFACE

		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragPortal
		//	ENDCG
		//}

		Pass
		{
			// Stencil prevents the backface from rendering if we've already seen the frontface
			// Don't render unless Stencil == 0. The value is decremented when we fail (saw a frontface)
			// so that a recursive layer above us won't be prevented from rendering.
			//Stencil{
			//	Ref 0
			//	Comp Equal
			//	Fail DecrSat
			//	Pass IncrSat
			//}	

			Blend One Zero
			ZWrite Off
			//ZTest LEqual
			ZTest Always
			Cull Back
			//Offset -0.1,-0.1

			CGPROGRAM
			#define IS_BACKFACE

			#include "PortalVRHelpers.cginc"

			#pragma multi_compile _ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE
			#pragma multi_compile _ PORTAL_WAVING_ENABLED

			#pragma vertex vertPortal
			#pragma fragment fragPortal
			ENDCG
		}
	}
	//FallBack "VertexLit"
}
