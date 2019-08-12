Shader "Portal/Portal"
{
	Properties
	{
		_DefaultTexture("DefaultTexture", 2D) = "white" {}
		_LeftEyeTexture("LeftEyeTexture", 2D) = "bump" {}
		_RightEyeTexture("RightEyeTexture", 2D) = "red" {}
		_TransparencyMask("TransparencyMask", 2D) = "white" {}
	}

	SubShader
	{
		Tags{
			"RenderType" = "Opaque"
			"Queue" = "Geometry+100"
			"IgnoreProjector" = "True"
		}

		//Pass
		//{
		//	Name "PortalStencilMask"

		//	ColorMask 0
		//	ZWrite Off
		//	Lighting Off
		//	ZTest Always
		//	Cull Front

		//	Stencil{
		//		Ref 1
		//		Comp Always
		//		Pass Replace
		//		Fail Zero
		//	}

		//	CGPROGRAM
		//	#pragma target 3.0

		//	#include "UnityCG.cginc"
		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragPortal

		//	ENDCG
		//}

		//Pass
		//{
		//	Name "PortalVisibleFaces"

		//	//Blend SrcAlpha OneMinusSrcAlpha
		//	ZWrite On
		//	Lighting Off
		//	Cull Back
		//	ZTest LEqual

		//	Stencil{
		//		Ref 0
		//		Comp Equal
		//		//Pass Zero
		//		//Fail Zero
		//	}

		//	CGPROGRAM
		//	#pragma target 3.0

		//	#include "UnityCG.cginc"
		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragPortal

		//	ENDCG
		//}

		//Pass
		//{
		//	Name "Portal Depth Only"

		//	ZWrite Off
		//	ColorMask 0
		//}

		Pass
		{
			Stencil {
				Ref 100
				Pass Replace
			}			
			Name "Portal ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			ZWrite Off
			ColorMask 0
		}

		//Pass
		//{
		//	Name "PORTAL DEFERRED"
		//	Tags { "LightMode" = "Deferred" }

		//	//Blend SrcAlpha OneMinusSrcAlpha
		//	ZWrite On
		//	ZTest Always
		//	Lighting Off

		//	CGPROGRAM
		//	#pragma target 3.0
		//	#pragma exclude_renderers nomrt
		//	
		//	#include "UnityCG.cginc"
		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragDeferred
		//	ENDCG
		//}

		//Pass
		//{
		//	// Stencil prevents the backface from rendering if we've already seen the frontface
		//	Stencil{
		//		Comp Always
		//		Pass IncrSat
		//		ZFail IncrSat
		//	}
		//	Blend SrcAlpha OneMinusSrcAlpha
		//	ZWrite Off
		//	Lighting Off

		//	CGPROGRAM
		//	#include "UnityCG.cginc"
		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragPortal
		//	ENDCG
		//}
	}
	FallBack "VertexLit"
}
