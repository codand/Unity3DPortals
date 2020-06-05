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

		Pass
		{
			// Stencil prevents the backface from rendering if we've already seen the frontface
			//Stencil{
			//	Comp Always
			//	Pass IncrSat
			//	ZFail IncrSat
			//}

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On
			ZTest LEqual
			Lighting Off
			Cull Back
			Offset -0.1, -0.1

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			#pragma multi_compile _ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

			#pragma vertex vertPortal
			#pragma fragment fragPortal
			ENDCG
		}


		Pass
		{
			Name "Portal ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			Cull Back

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			#pragma vertex vertPortalShadowCaster
			#pragma fragment fragPortalShadowCaster
			ENDCG
		}

		//UsePass "Standard/SHADOWCASTER"
	}
	//FallBack "VertexLit"
}
